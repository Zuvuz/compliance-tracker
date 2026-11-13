using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Audits;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Audits
{
    // Storage for audits (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IAuditRepository
    {
        // The audits of these projects, with their findings, newest first
        Task<IReadOnlyList<Audit>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

        Task AddAsync(Audit audit, AuditReportFile report, CancellationToken cancellationToken = default);

        Task<AuditReportFile?> GetReportAsync(Guid auditId, CancellationToken cancellationToken = default);
    }

    public record AuditFindingInput(string? Title, Guid OwnerId, DateOnly? DueOn);

    // What the form sends (R1–R3); the certificate fields are all empty when the audit gave none
    public record AuditInput(
        AuditKind Kind, string? Scope, DateOnly? HeldOn, string? AuditorName, string? AuditorCompany, AuditResult Result,
        string? CertificateNumber = null, string? CertificateIssuer = null, DateOnly? CertificateValidFrom = null, DateOnly? CertificateValidTo = null,
        Guid? CorrectsAuditId = null)
    {
        public IReadOnlyList<AuditFindingInput> Findings { get; init; } = [];
    }

    // A certificate as the header shows it (R3), from the project or the organisation
    public record CertificateBadge(string Text, CertificateState State, string Number, bool FromOrganisation);

    // US-134: the audits a project page shows: its own and the organisation's (B94)
    public record AuditOverview(IReadOnlyList<Audit> Own, IReadOnlyList<Audit> Organisation, DateOnly Today)
    {
        public IEnumerable<Audit> All => Own.Concat(Organisation);

        // R4: an audit that a later one corrects
        public Audit? CorrectedBy(Audit audit) => All.FirstOrDefault(a => a.CorrectsAuditId == audit.Id);

        // R3: the newest certificate per scope, of audits that are not corrected
        public IReadOnlyList<CertificateBadge> Badges => All
            .Where(a => a.HasCertificate && CorrectedBy(a) is null)
            .GroupBy(a => a.ShortScope, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(a => a.CertificateValidTo).First())
            .Select(a => new CertificateBadge(a.CertificateBadge(Today)!, a.CertificateOn(Today)!.Value, a.CertificateNumber!, Organisation.Contains(a)))
            .ToList();

        // R2: the open findings of the project's own audits (the organisation's are followed up in its area)
        public IReadOnlyList<AuditFinding> OpenFindings
            => Own.Where(a => CorrectedBy(a) is null).SelectMany(a => a.Findings).Where(f => f.IsOpen).OrderBy(f => f.DueOn).ToList();

        // R3: e.g. "Certificate DK-1234 expires in 42 days"
        public IReadOnlyList<string> Expiring => Badges.Where(b => b.State == CertificateState.ExpiringSoon)
            .Select(b => All.First(a => a.CertificateNumber == b.Number))
            .Select(a => $"Certificate {a.CertificateNumber} expires in {a.CertificateValidTo!.Value.DayNumber - Today.DayNumber} days")
            .ToList();

        public Audit? Latest => All.OrderByDescending(a => a.HeldOn).ThenByDescending(a => a.RecordedAt).FirstOrDefault();
    }

    // US-134: records audits with their report, findings and certificate, and follows the findings up
    public class AuditService
    {
        private readonly IProjectRepository _projects;
        private readonly IAuditRepository _audits;
        private readonly Compliance.IEvidenceRepository _evidence;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AuditService(IProjectRepository projects, IAuditRepository audits, Compliance.IEvidenceRepository evidence, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _audits = audits;
            _evidence = evidence;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1–R5: a compliance decision (DecideCompliance); the audit is never changed afterwards
        public async Task<Audit> RecordAsync(Guid projectId, AuditInput input, string? reportFileName, byte[] reportContent, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await OpenAsync(organization.Id, projectId, cancellationToken);
            if (reportContent.Length == 0)
            {
                throw new DomainException("Attach the auditor's report");
            }

            var report = EvidenceFile.Create(reportFileName ?? "audit-report.pdf", reportContent);
            if (input.CorrectsAuditId is { } corrected
                && (await _audits.ListAsync([project.Id], cancellationToken)).All(a => a.Id != corrected))
            {
                throw new DomainException("Only an audit of the same project can be corrected");
            }

            var today = DateOnly.FromDateTime(Now);
            var audit = Audit.Record(project.Id, input.Kind, input.Scope, input.HeldOn ?? throw new DomainException("Give the date of the audit"),
                input.AuditorName, input.AuditorCompany, input.Result, report.FileName, report.Sha256, user.Id, Now, today, input.CorrectsAuditId);

            if (!string.IsNullOrWhiteSpace(input.CertificateNumber) || input.CertificateValidTo is not null)
            {
                audit.GiveCertificate(input.CertificateNumber, input.CertificateIssuer,
                    input.CertificateValidFrom ?? audit.HeldOn, input.CertificateValidTo ?? throw new DomainException("Give the date the certificate is valid to"));
            }

            foreach (var finding in input.Findings.Where(f => !string.IsNullOrWhiteSpace(f.Title)))
            {
                if (organization.Members.All(m => m.Id != finding.OwnerId))
                {
                    throw new DomainException("A finding's owner must be a member of the organisation");
                }

                audit.AddFinding(finding.Title, finding.OwnerId, finding.DueOn ?? throw new DomainException("Give each finding a deadline"));
            }

            await _audits.AddAsync(audit, new AuditReportFile(audit.Id, report), cancellationToken);
            await _log.LogAsync(ActivityAction.AuditRecorded, project.Id, Describe(audit), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return audit;
        }

        // R2: closed once with a reason; evidence of the project may show it is fixed
        public async Task CloseFindingAsync(Guid projectId, Guid findingId, string? reason, Guid? evidenceId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await OpenAsync(organization.Id, projectId, cancellationToken);
            var finding = (await _audits.ListAsync([project.Id], cancellationToken)).SelectMany(a => a.Findings).FirstOrDefault(f => f.Id == findingId)
                ?? throw new DomainException("Finding not found");
            if (evidenceId is { } id && await _evidence.GetAsync(project.Id, id, cancellationToken) is null)
            {
                throw new DomainException("Evidence not found");
            }

            finding.Close(reason, evidenceId, user.Id, Now);
            await _log.LogAsync(ActivityAction.AuditFindingClosed, project.Id, $"{finding.Title}: {finding.CloseReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // For anyone in the organisation; null for another organisation's project
        public async Task<AuditOverview?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var area = project.IsOrganisationArea ? null
                : (await _projects.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(p => p.IsOrganisationArea);
            var audits = await _audits.ListAsync(area is null ? [project.Id] : [project.Id, area.Id], cancellationToken);
            return new AuditOverview(audits.Where(a => a.ProjectId == project.Id).ToList(), audits.Where(a => a.ProjectId != project.Id).ToList(),
                DateOnly.FromDateTime(Now));
        }

        // R1: the auditor's report, for anyone in the organisation (the project's or the organisation's audit)
        public async Task<(Audit Audit, AuditReportFile File)?> ReportAsync(Guid projectId, Guid auditId, CancellationToken cancellationToken = default)
        {
            if (await ForProjectAsync(projectId, cancellationToken) is not { } overview
                || overview.All.FirstOrDefault(a => a.Id == auditId) is not { } audit
                || await _audits.GetReportAsync(audit.Id, cancellationToken) is not { } file)
            {
                return null;
            }

            return (audit, file);
        }

        // R5: e.g. "External audit of ISO/IEC 27001:2022 by Bureau Veritas on 12-10-2026: PassedWithRemarks; certificate DK-1234 to 30-06-2027; 1 finding"
        public static string Describe(Audit audit)
            => $"{audit.Kind} audit of {audit.Scope} by {audit.AuditorCompany ?? audit.AuditorName} on {audit.HeldOn:dd-MM-yyyy}: {audit.Result}"
               + (audit.HasCertificate ? $"; certificate {audit.CertificateNumber} to {audit.CertificateValidTo:dd-MM-yyyy}" : "")
               + (audit.Findings.Count > 0 ? $"; {audit.Findings.Count} {(audit.Findings.Count == 1 ? "finding" : "findings")}" : "")
               + (audit.CorrectsAuditId is not null ? "; corrects an earlier record" : "");

        private async Task<Project> OpenAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(organizationId, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return project;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
