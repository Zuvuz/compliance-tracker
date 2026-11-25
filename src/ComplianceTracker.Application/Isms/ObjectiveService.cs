using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-209: storage for the objectives and their measurements (implemented in Infrastructure). Add only registers;
    // IUnitOfWork saves.
    public interface ISecurityObjectiveRepository
    {
        // The organisation's objectives with their measurements, removed ones too
        Task<IReadOnlyList<SecurityObjective>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // R2: the active objectives the tracker measures, in every organisation
        Task<IReadOnlyList<SecurityObjective>> ListTrackedAsync(CancellationToken cancellationToken = default);

        Task AddAsync(SecurityObjective objective, CancellationToken cancellationToken = default);
    }

    // An objective as typed on the page
    public sealed record ObjectiveInput(string? Text, Guid? OwnerId, TrackerMeasure? Measure, decimal? Target, string? Unit, ObjectiveDirection? Direction,
        ObjectiveFrequency? Frequency, DateOnly? DueOn);

    public sealed record ObjectivesView(IReadOnlyList<SecurityObjective> Objectives, IReadOnlyDictionary<Guid, string> Members, bool CanChange, DateOnly Today)
    {
        public string NameOf(Guid? id) => id is { } known ? Members.GetValueOrDefault(known) ?? "Former member" : "the tracker";
    }

    // US-209 R2, R5: the tracker's own measures, worked out over the projects inside the ISMS scope (US-208). Works without a
    // signed-in member, so the background job can use it. Null when there is nothing to measure.
    public class TrackerMeasures
    {
        private readonly IProjectRepository _projects;
        private readonly IIsmsScopeRepository _scopes;
        private readonly ComplianceQueries _compliance;
        private readonly IRiskRepository _risks;
        private readonly INonconformityRepository _nonconformities;
        private readonly ISecurityEventRepository _events;
        private readonly Organizations.IOrganizationRepository _organizations;
        private readonly Organizations.IOrganisationDocumentRepository _documents;
        private readonly ICompetenceRepository _competences;

        public TrackerMeasures(IProjectRepository projects, IIsmsScopeRepository scopes, ComplianceQueries compliance, IRiskRepository risks,
            INonconformityRepository nonconformities, ISecurityEventRepository events, Organizations.IOrganizationRepository organizations,
            Organizations.IOrganisationDocumentRepository documents, ICompetenceRepository competences)
        {
            _organizations = organizations;
            _documents = documents;
            _competences = competences;
            _projects = projects;
            _scopes = scopes;
            _compliance = compliance;
            _risks = risks;
            _nonconformities = nonconformities;
            _events = events;
        }

        public async Task<decimal?> MeasureAsync(Guid organizationId, TrackerMeasure measure, DateOnly today, CancellationToken cancellationToken = default)
        {
            if (measure == TrackerMeasure.OpenNonconformities)
            {
                return (await _nonconformities.ListAsync(organizationId, cancellationToken)).Count(n => !n.IsClosed);
            }

            // US-213 R6: the whole organisation's members and policies
            if (measure == TrackerMeasure.PoliciesRead)
            {
                if (await _organizations.GetByIdAsync(organizationId, cancellationToken) is not { } organization)
                {
                    return null;
                }

                var policies = (await _documents.ListAsync(organizationId, cancellationToken: cancellationToken))
                    .Where(d => d.IsActive && d.Kind == Domain.Organizations.OrganisationDocumentKind.Policy).ToList();
                return CompetenceService.ShareRead(policies, organization.Members, await _competences.ListAcknowledgementsAsync(organizationId, cancellationToken));
            }

            // R5: only the projects inside the scope; none inside, nothing to measure
            var scope = await _scopes.GetAsync(organizationId, cancellationToken);
            var projects = (await _projects.ListAsync(organizationId, cancellationToken))
                .Where(p => !p.IsOrganisationArea && !p.IsClosed && scope?.IsInside(p.Id) == true)
                .ToList();
            if (projects.Count == 0)
            {
                return null;
            }

            switch (measure)
            {
                case TrackerMeasure.RequirementCoverage:
                case TrackerMeasure.EvidenceCoverage:
                    var (part, total) = (0, 0);
                    foreach (var project in projects)
                    {
                        foreach (var framework in (await _compliance.ForProjectAsync(organizationId, project.Id, cancellationToken))?.Frameworks ?? [])
                        {
                            total += framework.Total;
                            part += measure == TrackerMeasure.RequirementCoverage ? framework.Covered : framework.WithEvidence;
                        }
                    }

                    return Percent(part, total);

                case TrackerMeasure.OpenHighRisks:
                case TrackerMeasure.OverdueRiskActions:
                    var risks = new List<Risk>();
                    foreach (var project in projects)
                    {
                        risks.AddRange((await _risks.ListAsync(project.Id, cancellationToken)).Where(r => !r.IsWithdrawn));
                    }

                    return measure == TrackerMeasure.OpenHighRisks
                        ? risks.Count(r => r.Status != RiskStatus.Closed && r.Level >= RiskLevel.High)
                        : risks.SelectMany(r => r.Actions).Count(a => a.Status != RiskActionStatus.Done && a.DueOn < today);

                case TrackerMeasure.OpenSecurityEvents:
                    var open = 0;
                    foreach (var project in projects)
                    {
                        open += (await _events.ListAsync(project.Id, cancellationToken)).Count(e => e.IsOpen);
                    }

                    return open;

                default:
                    return null;
            }
        }

        // Whole per cent, like the coverage elsewhere; null when there are no requirements
        public static decimal? Percent(int part, int total)
            => total == 0 ? null : Math.Round(100m * part / total, 0, MidpointRounding.AwayFromZero);
    }

    public class ObjectiveService
    {
        private readonly ISecurityObjectiveRepository _objectives;
        private readonly TrackerMeasures _measures;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ObjectiveService(ISecurityObjectiveRepository objectives, TrackerMeasures measures, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _objectives = objectives;
            _measures = measures;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member reads the objectives: the off-target first, then the due, then by text
        public async Task<ObjectivesView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var objectives = (await _objectives.ListAsync(organization.Id, cancellationToken)).Where(o => o.IsActive)
                .OrderByDescending(o => o.State)
                .ThenByDescending(o => o.IsManual && o.IsDueForMeasurement(Today))
                .ThenBy(o => o.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new ObjectivesView(objectives, organization.Users.ToDictionary(u => u.Id, u => u.Name),
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // R1
        public async Task<SecurityObjective> AddAsync(ObjectiveInput input, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            RequireMember(organization, input.OwnerId);
            var objective = SecurityObjective.Create(organization.Id, input.Text, input.OwnerId, input.Measure, input.Target, input.Unit, input.Direction,
                input.Frequency, input.DueOn, Now);
            await _objectives.AddAsync(objective, cancellationToken);
            await _log.LogAsync(ActivityAction.ObjectiveChanged, organization.Id,
                $"Added {objective.Text} ({Target(objective)}, {(objective.Measure is { } measure ? $"measured by the tracker: {Label(measure)}" : "measured by hand")})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return objective;
        }

        public async Task ChangeAsync(Guid objectiveId, ObjectiveInput input, CancellationToken cancellationToken = default)
        {
            var (organization, objective) = await ObjectiveAsync(objectiveId, cancellationToken);
            if (input.OwnerId != objective.OwnerId)
            {
                RequireMember(organization, input.OwnerId);
            }

            var changed = objective.Change(input.Text, input.OwnerId, input.Target, input.Unit, input.Direction, input.Frequency, input.DueOn);
            if (changed.Count == 0)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.ObjectiveChanged, organization.Id, $"Changed {objective.Text}: {string.Join(", ", changed)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveAsync(Guid objectiveId, CancellationToken cancellationToken = default)
        {
            var (organization, objective) = await ObjectiveAsync(objectiveId, cancellationToken);
            objective.Remove(Now);
            await _log.LogAsync(ActivityAction.ObjectiveChanged, organization.Id, $"Removed {objective.Text}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3, R6: a person's measurement; on a tracker objective it is a correction with a note
        public async Task RecordAsync(Guid objectiveId, decimal? value, string? note, CancellationToken cancellationToken = default)
        {
            var (organization, objective) = await ObjectiveAsync(objectiveId, cancellationToken);
            var (_, user) = await _member.RequireAsync(cancellationToken);
            var measurement = objective.Record(value, Today, user.Id, note, Now);
            await _log.LogAsync(ActivityAction.ObjectiveChanged, organization.Id,
                $"Measured {objective.Text}: {Format(measurement.Value, objective.Unit)}{(measurement.Note is { } n ? $" ({n})" : "")}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: the background job. Every objective the tracker measures and that is due gets a measurement; one that cannot be
        // measured (no project inside the scope, no requirements) waits for the next run. Returns how many were taken.
        public async Task<int> MeasureDueAsync(CancellationToken cancellationToken = default)
        {
            var taken = 0;
            foreach (var objective in (await _objectives.ListTrackedAsync(cancellationToken)).Where(o => o.IsDueForMeasurement(Today)))
            {
                if (await _measures.MeasureAsync(objective.OrganizationId, objective.Measure!.Value, Today, cancellationToken) is { } value)
                {
                    objective.Record(value, Today, null, null, Now);
                    taken++;
                }
            }

            if (taken > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return taken;
        }

        public static string Label(TrackerMeasure measure) => measure switch
        {
            TrackerMeasure.RequirementCoverage => "Requirement coverage",
            TrackerMeasure.EvidenceCoverage => "Requirements with evidence",
            TrackerMeasure.OpenHighRisks => "Open high and critical risks",
            TrackerMeasure.OverdueRiskActions => "Overdue risk actions",
            TrackerMeasure.OpenNonconformities => "Open nonconformities",
            TrackerMeasure.PoliciesRead => "Policies read by the members",
            _ => "Open security events"
        };

        // e.g. "86 %", "4", "3.5 days"
        public static string Format(decimal value, string unit)
        {
            var number = value == Math.Round(value) ? ((int)value).ToString() : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return unit.Length == 0 ? number : $"{number} {unit}";
        }

        // e.g. "≥ 90 %", "≤ 2"
        public static string Target(SecurityObjective objective)
            => $"{(objective.Direction == ObjectiveDirection.HigherIsBetter ? "≥" : "≤")} {Format(objective.Target, objective.Unit)}";

        private async Task<(Domain.Organizations.Organization, SecurityObjective)> ObjectiveAsync(Guid objectiveId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var objective = (await _objectives.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(o => o.Id == objectiveId && o.IsActive)
                            ?? throw new DomainException("Objective not found");
            return (organization, objective);
        }

        private static void RequireMember(Domain.Organizations.Organization organization, Guid? ownerId)
        {
            if (ownerId is { } id && organization.Members.All(m => m.Id != id))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }
        }
    }
}
