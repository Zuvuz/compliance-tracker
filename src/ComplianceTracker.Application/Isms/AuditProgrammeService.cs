using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-211: storage for the audit programme (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IAuditProgrammeRepository
    {
        Task<AuditProgramme?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(AuditProgramme programme, CancellationToken cancellationToken = default);
    }

    // A recorded audit (US-134) a planned audit can be linked to
    public sealed record RecordedAuditOption(Guid Id, string Label);

    public sealed record AuditProgrammeView(
        AuditProgramme? Programme,
        IReadOnlyList<(string Item, CoverageState State)> Coverage,
        IReadOnlyList<string> ApplicableControls,
        IReadOnlyDictionary<Guid, string> Members,
        IReadOnlyList<(Guid Id, string Name)> Projects,
        IReadOnlyList<RecordedAuditOption> RecordedAudits,
        bool CanChange,
        DateOnly Today)
    {
        public string NameOf(Guid? id) => id is { } known ? Members.GetValueOrDefault(known) ?? "Former member" : "";

        public string AuditorOf(PlannedAudit audit) => audit.AuditorId is { } id ? NameOf(id) : audit.AuditorName ?? "";

        public string ProjectsOf(PlannedAudit audit)
            => string.Join(", ", Projects.Where(p => audit.ProjectIds.Contains(p.Id)).Select(p => p.Name));

        // R3: e.g. "Missing: 9, 10 and 53 controls"
        public string? MissingText
        {
            get
            {
                var missing = Coverage.Where(c => c.State == CoverageState.Missing).Select(c => c.Item).ToList();
                if (missing.Count == 0)
                {
                    return null;
                }

                var clauses = missing.Where(m => AuditProgramme.Clauses.Contains(m)).ToList();
                var controls = missing.Count - clauses.Count;
                var parts = clauses.Select(c => c).ToList();
                if (controls > 0)
                {
                    parts.Add($"{controls} control{(controls == 1 ? "" : "s")}");
                }

                return "Missing: " + (parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts[..^1])} and {parts[^1]}");
            }
        }
    }

    public class AuditProgrammeService
    {
        private readonly IAuditProgrammeRepository _programmes;
        private readonly IProjectRepository _projects;
        private readonly IAuditRepository _audits;
        private readonly INonconformityRepository _nonconformities;
        private readonly StatementOfApplicabilityQueries _soa;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AuditProgrammeService(IAuditProgrammeRepository programmes, IProjectRepository projects, IAuditRepository audits,
            INonconformityRepository nonconformities, StatementOfApplicabilityQueries soa, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _programmes = programmes;
            _projects = projects;
            _audits = audits;
            _nonconformities = nonconformities;
            _soa = soa;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member reads the programme
        public async Task<AuditProgrammeView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var programme = await _programmes.GetAsync(organization.Id, cancellationToken);

            // R3: the controls that apply in the Statement of Applicability (US-053), when the organisation has ISO/IEC 27001
            var applicable = await _soa.ForOrganizationAsync(cancellationToken) is { } soa
                ? StatementOfApplicability.Controls(soa.Projects).Where(c => c.Applicable).Select(c => c.Code).ToList()
                : [];
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsOrganisationArea && !p.IsClosed).ToList();
            var linked = programme?.Audits.Select(a => a.RecordedAuditId).OfType<Guid>().ToHashSet() ?? [];
            var names = projects.ToDictionary(p => p.Id, p => p.Name);
            var recorded = (await _audits.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken))
                .Where(a => !linked.Contains(a.Id))
                .OrderByDescending(a => a.HeldOn)
                .Select(a => new RecordedAuditOption(a.Id, $"{a.HeldOn:dd-MM-yyyy} {names.GetValueOrDefault(a.ProjectId, "")}: {a.Scope} ({a.Kind})"))
                .ToList();
            return new AuditProgrammeView(programme, programme?.Coverage(applicable) ?? [], applicable,
                organization.Users.ToDictionary(u => u.Id, u => u.Name),
                projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => (p.Id, p.Name)).ToList(),
                recorded, await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // R1: starts or changes the programme
        public async Task SetCycleAsync(DateOnly? startsOn, int? years, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var programme = await _programmes.GetAsync(organization.Id, cancellationToken);
            if (programme is null)
            {
                programme = AuditProgramme.Create(organization.Id, startsOn, years);
                await _programmes.AddAsync(programme, cancellationToken);
            }
            else
            {
                programme.Change(startsOn, years);
            }

            await _log.LogAsync(ActivityAction.AuditProgrammeChanged, organization.Id,
                $"Programme {programme.StartsOn:dd-MM-yyyy} to {programme.EndsOn:dd-MM-yyyy} ({programme.Years} year{(programme.Years == 1 ? "" : "s")})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R4: the owner of a control
        public async Task SetOwnerAsync(string? code, Guid? ownerId, CancellationToken cancellationToken = default)
        {
            var (organization, programme) = await ProgrammeAsync(cancellationToken);
            if (ownerId is { } owner && organization.Members.All(m => m.Id != owner))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }

            programme.SetOwner(code, ownerId);
            var clean = AuditProgramme.ControlCode(code)!;
            await _log.LogAsync(ActivityAction.AuditProgrammeChanged, organization.Id,
                ownerId is { } id ? $"{clean} owned by {organization.Users.First(u => u.Id == id).Name}" : $"{clean} has no owner", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2, R4
        public async Task<PlannedAudit> PlanAsync(DateOnly? on, IReadOnlyList<string?>? clauses, string? controls, IReadOnlyList<Guid>? projectIds,
            Guid? auditorId, string? auditorName, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, programme) = await ProgrammeAsync(cancellationToken);
            if (auditorId is { } auditor && organization.Members.All(m => m.Id != auditor))
            {
                throw new DomainException("The auditor must be a member of the organisation, or write the name of an external one");
            }

            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Select(p => p.Id).ToHashSet();
            if ((projectIds ?? []).Any(p => !projects.Contains(p)))
            {
                throw new DomainException("Choose projects from the list");
            }

            var label = auditorId is { } a ? organization.Users.First(u => u.Id == a).Name : (auditorName ?? "").Trim();
            var planned = programme.Plan(on, clauses, Split(controls), projectIds, auditorId, auditorName, label, reason);
            await _log.LogAsync(ActivityAction.AuditProgrammeChanged, organization.Id,
                $"Planned an audit on {planned.On:dd-MM-yyyy} of {string.Join(", ", planned.Clauses.Select(c => $"clause {c}").Concat(planned.Controls))} by {label}"
                + (planned.ImpartialityReason is { } why ? $"; kept though {label} owns a control, because {why}" : ""),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return planned;
        }

        public async Task RemoveAsync(Guid plannedId, CancellationToken cancellationToken = default)
        {
            var (organization, programme) = await ProgrammeAsync(cancellationToken);
            var removed = programme.Remove(plannedId);
            await _log.LogAsync(ActivityAction.AuditProgrammeChanged, organization.Id, $"Removed the audit planned on {removed.On:dd-MM-yyyy}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: the planned audit is done with the recorded audit, and its open findings go to the nonconformity register
        // (US-212); a finding that already has an open nonconformity gets no second one. Returns how many were raised.
        public async Task<int> LinkAsync(Guid plannedId, Guid recordedAuditId, CancellationToken cancellationToken = default)
        {
            var (organization, programme) = await ProgrammeAsync(cancellationToken);
            var (_, user) = await _member.RequireAsync(cancellationToken);
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Select(p => p.Id).ToList();
            var recorded = (await _audits.ListAsync(projects, cancellationToken)).FirstOrDefault(a => a.Id == recordedAuditId)
                           ?? throw new DomainException("Choose a recorded audit from the list");
            if (programme.Audits.Any(a => a.RecordedAuditId == recorded.Id))
            {
                throw new DomainException("That recorded audit is linked to another planned audit");
            }

            var planned = programme.AuditOf(plannedId);
            planned.MarkDone(recorded.Id);

            var open = (await _nonconformities.ListAsync(organization.Id, cancellationToken)).Where(n => !n.IsClosed).Select(n => n.SourceId).ToHashSet();
            var raised = 0;
            foreach (var finding in recorded.Findings.Where(f => f.IsOpen && !open.Contains(f.Id)))
            {
                await _nonconformities.AddAsync(Nonconformity.Raise(organization.Id, finding.Title, $"Found in the internal audit on {recorded.HeldOn:dd-MM-yyyy}",
                    NonconformitySource.InternalAudit, finding.Id, NonconformitySeverity.Minor, user.Id, Now), cancellationToken);
                raised++;
            }

            await _log.LogAsync(ActivityAction.AuditProgrammeChanged, organization.Id,
                $"The audit planned on {planned.On:dd-MM-yyyy} is done; {raised} finding{(raised == 1 ? "" : "s")} went to the nonconformities", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return raised;
        }

        // "A.8.13, A.5.1; A.5.2" → the codes
        public static IReadOnlyList<string> Split(string? controls)
            => (controls ?? "").Split([',', ';', '\n'],StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private async Task<(Domain.Organizations.Organization, AuditProgramme)> ProgrammeAsync(CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var programme = await _programmes.GetAsync(organization.Id, cancellationToken)
                            ?? throw new DomainException("Start the audit programme first");
            return (organization, programme);
        }
    }
}
