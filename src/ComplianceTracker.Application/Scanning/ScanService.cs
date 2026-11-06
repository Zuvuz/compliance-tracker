using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Scanning
{
    // US-078: what a scanner reported, read from its file. One importer per format (B36, like US-083's readers).
    public record ScanReport(string Tool, IReadOnlyList<ScanResult> Results);

    public record ScanResult(string Fingerprint, string RuleId, FindingSeverity Severity, string Message, string? Location);

    public interface IScanImporter
    {
        // e.g. "SARIF 2.1.0"
        string Format { get; }

        // Whether the file is in this importer's format (by its name and content)
        bool CanRead(string fileName, byte[] content);

        // Throws DomainException with a message for the user when the file cannot be read
        ScanReport Read(byte[] content);
    }

    // Storage for scans and findings (implemented in Infrastructure)
    public interface IScanRepository
    {
        // Newest first
        Task<IReadOnlyList<Scan>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<Scan?> GetAsync(Guid projectId, Guid scanId, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid scanId, CancellationToken cancellationToken = default);

        Task AddAsync(Scan scan, byte[] content, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Finding>> ListFindingsAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddFindingsAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken = default);
    }

    // What the Scans tab reads (R6: every member, also the Auditor)
    public record ScanOverview(IReadOnlyList<Scan> LatestPerTool, IReadOnlyList<Finding> Open, IReadOnlyList<Finding> Closed)
    {
        public int OpenAtLeastHigh => Open.Count(f => f.Severity >= FindingSeverity.High);
    }

    public class ScanService
    {
        private readonly IProjectRepository _projects;
        private readonly IScanRepository _scans;
        private readonly IEnumerable<IScanImporter> _importers;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ScanService(
            IProjectRepository projects, IScanRepository scans, IEnumerable<IScanImporter> importers, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _scans = scans;
            _importers = importers;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1–R3, R7: reads the file and matches its results with the tool's findings by fingerprint
        public async Task<Scan> UploadAsync(Guid projectId, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            if (content.Length > Scan.MaxSizeInBytes)
            {
                throw new DomainException("The scan is larger than 50 MB");
            }

            var importer = _importers.FirstOrDefault(i => i.CanRead(fileName, content))
                ?? throw new DomainException("Only SARIF files can be uploaded");
            var report = importer.Read(content);

            var scan = Scan.Create(project.Id, report.Tool, fileName, content, userId, Now);
            var existing = (await _scans.ListFindingsAsync(project.Id, cancellationToken))
                .Where(f => string.Equals(f.Tool, scan.Tool, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(f => f.Fingerprint);

            var reported = report.Results.GroupBy(r => r.Fingerprint).Select(g => g.First()).ToList();
            var added = new List<Finding>();
            foreach (var result in reported)
            {
                if (existing.TryGetValue(result.Fingerprint, out var finding))
                {
                    finding.SeenIn(scan.Id);
                }
                else
                {
                    added.Add(Finding.Create(project.Id, scan.Tool, result.Fingerprint, result.RuleId, result.Severity, result.Message, result.Location, scan.Id));
                }
            }

            var fingerprints = reported.Select(r => r.Fingerprint).ToHashSet();
            var fixedCount = existing.Values.Where(f => !fingerprints.Contains(f.Fingerprint)).Count(f => f.MissingFrom(scan.Id, Now));
            var open = existing.Values.Where(f => f.IsOpen).Concat(added).ToList();
            scan.Record(added.Count, fixedCount, open.Count, open.Count(f => f.Severity == FindingSeverity.Critical));

            await _scans.AddAsync(scan, content, cancellationToken);
            await _scans.AddFindingsAsync(added, cancellationToken);
            await _log.LogAsync(ActivityAction.ScanImported, project.Id, scan.Summary, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return scan;
        }

        // R4: accepted with a reason; logged
        public async Task AcceptFindingAsync(Guid projectId, Guid findingId, string? reason, CancellationToken cancellationToken = default)
        {
            var (userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            var finding = (await _scans.ListFindingsAsync(project.Id, cancellationToken)).FirstOrDefault(f => f.Id == findingId)
                ?? throw new DomainException("Finding not found");

            finding.Accept(reason, userId, Now);
            await _log.LogAsync(ActivityAction.FindingAccepted, project.Id, $"{finding.RuleId} accepted: {finding.AcceptedReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The Scans tab; null for other organisations' projects
        public async Task<ScanOverview?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var scans = await _scans.ListAsync(project.Id, cancellationToken);
            var findings = await _scans.ListFindingsAsync(project.Id, cancellationToken);
            return new ScanOverview(
                scans.GroupBy(s => s.Tool, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(s => s.ScannedAt).First()).ToList(),
                findings.Where(f => f.IsOpen).OrderByDescending(f => f.Severity).ThenBy(f => f.RuleId, StringComparer.Ordinal).ToList(),
                findings.Where(f => !f.IsOpen).OrderByDescending(f => f.FixedAt ?? f.AcceptedAt).ToList());
        }

        // R5: the scan file, to add it as evidence (US-057); null for other organisations' projects
        public async Task<(Scan Scan, byte[] Content)?> FileAsync(Guid projectId, Guid scanId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || await _scans.GetAsync(project.Id, scanId, cancellationToken) is not { } scan
                || await _scans.GetContentAsync(scan.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (scan, content);
        }

        // R6: LinkWork; US-024 R4: nothing changes at end of support
        private async Task<(Guid UserId, Domain.Projects.Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (user.Id, project);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
