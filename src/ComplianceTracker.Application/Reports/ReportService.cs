using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Reports
{
    // Storage for locked reports (US-050, implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IReportSnapshotRepository
    {
        // The project's locked reports, newest first
        Task<IReadOnlyList<ReportSnapshot>> ListAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);

        Task<ReportSnapshot?> GetAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default);

        // R4: the organisation's snapshots with this fingerprint
        Task<IReadOnlyList<ReportSnapshot>> FindAsync(Guid organizationId, string sha256, CancellationToken cancellationToken = default);

        Task AddAsync(ReportSnapshot snapshot, byte[] content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid snapshotId, CancellationToken cancellationToken = default);
    }

    // A report file as it is made right now
    // Figures: US-141 R3, the management report's coverage per regulation as JSON, kept when it is locked
    public record ReportFile(string Kind, string Title, string FileName, string ContentType, byte[] Content, string? Figures = null);

    // US-131: makes the reports' PDFs. The download and the lock (US-050) use the same code, so a locked PDF is exactly the
    // report a member downloads. Null when the member's organisation does not own the project, or the report cannot be made.
    public class ReportFiles
    {
        private readonly ProjectQueries _projects;
        private readonly OrganizationQueries _organizations;
        private readonly ComplianceQueries _compliance;
        private readonly RiskQueries _risks;
        private readonly ScanService _scans;
        private readonly SecurityEventQueries _events;
        private readonly TechnicalDocumentationQueries _drafts;
        private readonly IPdfWriter _pdf;
        private readonly IReportSnapshotRepository _snapshots;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _clock;

        public ReportFiles(
            ProjectQueries projects, OrganizationQueries organizations, ComplianceQueries compliance, RiskQueries risks, ScanService scans,
            SecurityEventQueries events, TechnicalDocumentationQueries drafts, IPdfWriter pdf, IReportSnapshotRepository snapshots,
            ICurrentUser currentUser, TimeProvider clock)
        {
            _snapshots = snapshots;
            _projects = projects;
            _organizations = organizations;
            _compliance = compliance;
            _risks = risks;
            _scans = scans;
            _events = events;
            _drafts = drafts;
            _pdf = pdf;
            _currentUser = currentUser;
            _clock = clock;
        }

        // US-120: for one reader, for all regulations or one (sub is its slug)
        public async Task<(ReportDocument Document, ReportFile Pdf)?> TraceabilityAsync(
            Guid projectId, ReportAudience audience, string? sub, CancellationToken cancellationToken = default)
        {
            if (await BuildTraceabilityAsync(projectId, audience, sub, cancellationToken) is not { } built)
            {
                return null;
            }

            var (document, project, organization, frameworks, now) = built;
            var reader = audience.ToString().ToLowerInvariant();
            var name = $"traceability-{FrameworkSlugs.Slug(project.Name)}{(sub is null ? "" : "-" + sub)}-{reader}-{_clock.Today():yyyy-MM-dd}.pdf";
            var title = $"{document.Title} ({TraceabilityReport.Title(audience)}{(sub is null ? "" : ", " + string.Join(", ", frameworks.Select(f => FrameworkSlugs.Short(f.Framework.Name))))})";
            var header = new PdfHeader(organization.Name, project.Name, title, now, _currentUser.Identity.Name);
            return (document, new ReportFile($"traceability:{reader}:{sub ?? "all"}", title, name, "application/pdf", _pdf.FromReport(document, header),
                document.Figures is { } figures ? ManagementPriorities.FiguresJson(figures) : null));
        }

        // US-141: the report model only, for the Excel file, so Excel and PDF are made by the same code (R8)
        public async Task<ReportDocument?> TraceabilityDocumentAsync(
            Guid projectId, ReportAudience audience, string? sub, CancellationToken cancellationToken = default)
            => (await BuildTraceabilityAsync(projectId, audience, sub, cancellationToken))?.Document;

        private async Task<(ReportDocument Document, Domain.Projects.Project Project, Domain.Organizations.Organization Organization,
            IReadOnlyList<FrameworkCompliance> Frameworks, DateTime Now)?> BuildTraceabilityAsync(
            Guid projectId, ReportAudience audience, string? sub, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            var compliance = await _compliance.ForProjectAsync(projectId, cancellationToken);
            if (project is null || organization is null || compliance is null)
            {
                return null;
            }

            var frameworks = compliance.Frameworks.Where(f => sub is null || FrameworkSlugs.Slug(f.Framework.Name) == sub).ToList();
            if (sub is not null && frameworks.Count == 0)
            {
                return null;
            }

            string NameOf(Guid userId) => organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";
            var now = _clock.GetUtcNow().UtcDateTime;
            var highRisks = (await _risks.ForProjectAsync(projectId, cancellationToken) ?? [])
                .Where(r => r.Status != RiskStatus.Closed && r.Level >= RiskLevel.High)
                .Select(r =>
                {
                    var owner = organization.Users.FirstOrDefault(u => u.Id == r.OwnerId);
                    var due = r.Actions.Where(a => a.Status != RiskActionStatus.Done && a.DueOn is not null).Select(a => a.DueOn).Min();
                    return new ManagementRisk(r.Title, r.Level, r.Score, owner?.Name ?? "", owner is not null, due);
                })
                .ToList();
            var facts = new ManagementFacts(
                highRisks.Count,
                (await _scans.ForProjectAsync(projectId, cancellationToken))?.OpenAtLeastHigh ?? 0,
                (await _events.ForProjectAsync(projectId, cancellationToken) ?? []).Count(e => e.IsOpen),
                project.SupportEndsOn)
            {
                HighRisks = highRisks,
                Last = audience == ReportAudience.Management
                    ? await LastManagementReportAsync(organization.Id, projectId, frameworks.Select(f => f.Framework.Name).Distinct().ToList(), cancellationToken)
                    : null
            };
            var document = TraceabilityReport.Build(audience, project, frameworks, NameOf, _currentUser.Identity.Name, now, facts);
            return (document, project, organization, frameworks, now);
        }

        // US-141 R3: the newest locked management report of the project with exactly these regulations
        private async Task<LastManagementReport?> LastManagementReportAsync(
            Guid organizationId, Guid projectId, IReadOnlyList<string> regulations, CancellationToken cancellationToken)
        {
            foreach (var snapshot in (await _snapshots.ListAsync(organizationId, projectId, cancellationToken)).OrderByDescending(s => s.LockedAt))
            {
                if (snapshot.Kind.StartsWith("traceability:management:", StringComparison.Ordinal)
                    && ManagementPriorities.ParseFigures(snapshot.Figures) is { } figures
                    && ManagementPriorities.SameRegulations(figures, regulations))
                {
                    return new LastManagementReport(snapshot.LockedAt, figures);
                }
            }

            return null;
        }

        // US-056
        public async Task<(GeneratedDocument Draft, ReportFile Pdf)?> TechnicalDocumentationAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var draft = await _drafts.DraftAsync(projectId, cancellationToken);
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (draft is null || project is null || organization is null)
            {
                return null;
            }

            const string title = "Technical documentation (draft)";
            var header = new PdfHeader(organization.Name, project.Name, title, _clock.GetUtcNow().UtcDateTime, _currentUser.Identity.Name);
            return (draft, new ReportFile("technical-documentation", $"{title} – {project.Name}", Path.ChangeExtension(draft.FileName, ".pdf"),
                "application/pdf", _pdf.FromDocument(draft, header)));
        }
    }

    // What the Reports tab shows of a locked report
    public record LockedReport(ReportSnapshot Snapshot, string LockedBy);

    // US-050 R4: the result of checking a file
    public record FingerprintCheck(string Sha256, ReportSnapshot? Match);

    // US-050: lock a report as PDF, list the locked ones, download one, and check a file against them
    public class ReportService
    {
        private readonly IReportSnapshotRepository _snapshots;
        private readonly ReportFiles _files;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ReportService(
            IReportSnapshotRepository snapshots, ReportFiles files, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _snapshots = snapshots;
            _files = files;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R5, R6
        public async Task<ReportSnapshot> LockTraceabilityAsync(Guid projectId, ReportAudience audience, string? sub, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var made = await _files.TraceabilityAsync(projectId, audience, sub, cancellationToken)
                ?? throw new DomainException("The report cannot be made for this project");
            return await LockAsync(organization.Id, projectId, user, made.Pdf, cancellationToken);
        }

        public async Task<ReportSnapshot> LockTechnicalDocumentationAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var made = await _files.TechnicalDocumentationAsync(projectId, cancellationToken)
                ?? throw new DomainException("The report cannot be made for this project");
            return await LockAsync(organization.Id, projectId, user, made.Pdf, cancellationToken);
        }

        // R3: for anyone in the organisation, also the Auditor; empty for other organisations
        public async Task<IReadOnlyList<LockedReport>> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            return (await _snapshots.ListAsync(organization.Id, projectId, cancellationToken))
                .Select(s => new LockedReport(s, organization.Users.FirstOrDefault(u => u.Id == s.LockedById)?.Name ?? "?"))
                .ToList();
        }

        public async Task<(ReportSnapshot Snapshot, byte[] Content)?> DownloadAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _snapshots.GetAsync(organization.Id, snapshotId, cancellationToken) is not { } snapshot
                || await _snapshots.GetContentAsync(snapshot.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (snapshot, content);
        }

        // R4: any member may check a file; only the organisation's own snapshots are compared
        public async Task<FingerprintCheck> CheckAsync(byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            if (content.Length == 0)
            {
                throw new DomainException("Choose the file to check");
            }

            var sha = ReportSnapshot.Fingerprint(content);
            var match = (await _snapshots.FindAsync(organization.Id, sha, cancellationToken)).OrderBy(s => s.LockedAt).FirstOrDefault();
            return new FingerprintCheck(sha, match);
        }

        private async Task<ReportSnapshot> LockAsync(Guid organizationId, Guid projectId, User user, ReportFile file, CancellationToken cancellationToken)
        {
            var snapshot = ReportSnapshot.Lock(organizationId, projectId, file.Kind, file.Title, file.FileName, file.ContentType, file.Content,
                user.Id, user.Role, _clock.GetUtcNow().UtcDateTime, file.Figures);
            await _snapshots.AddAsync(snapshot, file.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.ReportLocked, projectId, $"{snapshot.Title}: SHA-256 {snapshot.Sha256}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return snapshot;
        }
    }
}
