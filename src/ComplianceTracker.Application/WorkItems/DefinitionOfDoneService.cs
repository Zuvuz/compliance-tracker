using System.Globalization;
using System.Text;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // US-109: storage for the Definition of Done and the manual ticks (implemented in Infrastructure)
    public interface IDefinitionOfDoneRepository
    {
        Task<DefinitionOfDone?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(DefinitionOfDone definition, CancellationToken cancellationToken = default);

        // Every tick of the project, unticked ones too (the history)
        Task<IReadOnlyList<DoneTick>> ListTicksAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddTickAsync(DoneTick tick, CancellationToken cancellationToken = default);
    }

    public enum CriterionState
    {
        Met,
        NotMet,
        NotChecked
    }

    // R3, R4: one criterion of one work item, with why it is not met, or who ticked it
    public sealed record CriterionStatus(DoneCriterion Criterion, CriterionState State, string? Note = null);

    // R6: one work item with its criteria; it meets the Definition of Done when none is "not met"
    public sealed record DoneRow(WorkItem Item, IReadOnlyList<CriterionStatus> Criteria)
    {
        public bool Meets => Criteria.All(c => c.State != CriterionState.NotMet);
        public IEnumerable<CriterionStatus> Missing => Criteria.Where(c => c.State == CriterionState.NotMet);
    }

    // The audit of a project: every work item it uses with its criteria; the finished ones are what an auditor looks at
    public sealed record DoneAuditView(Project Project, DefinitionOfDone Definition, IReadOnlyList<DoneRow> Rows)
    {
        public IReadOnlyList<DoneRow> Finished => Rows.Where(r => r.Item.IsFinished).OrderByDescending(r => r.Item.FinishedAt).ToList();

        // R9: finished, but not meeting it
        public int FinishedNotMeeting => Rows.Count(r => r.Item.IsFinished && !r.Meets);

        public DoneRow? Of(Guid workItemId) => Rows.FirstOrDefault(r => r.Item.Id == workItemId);
    }

    // US-109 R2–R4: a work item's criteria from the tracker's own data and the manual ticks. Pure, so the edge cases are tested.
    public static class DoneAudit
    {
        public static DoneRow For(WorkItem item, IReadOnlyList<DoneCriterion> criteria, ProjectCompliance? compliance, IReadOnlyList<DoneTick> ticks,
            IReadOnlyDictionary<Guid, string>? names = null)
        {
            var rows = (compliance?.Frameworks ?? []).SelectMany(f => f.Requirements)
                .Where(r => r.IsApplicable && r.Links.Any(l => l.WorkItem.Id == item.Id && !l.Link.NeedsReview)).ToList();
            var testsKnown = (compliance?.Frameworks ?? []).SelectMany(f => f.Requirements)
                .Any(r => r.FailingTests > 0 || r.Evidence.Any(e => e.AcceptedFrom == Domain.Mappings.EvidenceSourceKind.TestRun));
            return new DoneRow(item, criteria.Select(c => Status(c, item, rows, testsKnown, compliance, ticks, names)).ToList());
        }

        private static CriterionStatus Status(DoneCriterion criterion, WorkItem item, IReadOnlyList<RequirementCoverage> rows, bool testsKnown,
            ProjectCompliance? compliance, IReadOnlyList<DoneTick> ticks, IReadOnlyDictionary<Guid, string>? names)
        {
            switch (criterion.Check)
            {
                case DoneCheck.Manual:
                    var tick = ticks.Where(t => t.WorkItemId == item.Id && string.Equals(t.Criterion, criterion.Title, StringComparison.OrdinalIgnoreCase) && t.CountsFor(item))
                        .OrderByDescending(t => t.TickedAt).FirstOrDefault();
                    return tick is null
                        ? new CriterionStatus(criterion, CriterionState.NotMet, "Not ticked")
                        : new CriterionStatus(criterion, CriterionState.Met,
                            $"Ticked by {names?.GetValueOrDefault(tick.TickedById) ?? "a member"} on {tick.TickedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}");

                case DoneCheck.TemplateFilled:
                    var empty = EmptySections(item);
                    return empty.Count == 0 ? new CriterionStatus(criterion, CriterionState.Met)
                        : new CriterionStatus(criterion, CriterionState.NotMet, $"Empty: {string.Join(", ", empty)}");

                case DoneCheck.Linked:
                    return rows.Count > 0 ? new CriterionStatus(criterion, CriterionState.Met, string.Join(", ", rows.Select(r => r.Requirement.Code)))
                        : new CriterionStatus(criterion, CriterionState.NotMet, "No requirement is linked");

                case DoneCheck.Evidence:
                    if (rows.Count == 0)
                    {
                        return new CriterionStatus(criterion, CriterionState.NotMet, "No requirement is linked");
                    }

                    var unproven = rows.Where(r => !r.IsProven)
                        .Select(r => $"{r.Requirement.Code}: {(r.IsDocumentedOnly ? "only documentation" : r.Evidence.Any(e => e.IsExpired) ? "expired" : "no evidence")}").ToList();
                    return unproven.Count == 0 ? new CriterionStatus(criterion, CriterionState.Met)
                        : new CriterionStatus(criterion, CriterionState.NotMet, string.Join("; ", unproven));

                case DoneCheck.TestsPassed:
                    if (!testsKnown)
                    {
                        return new CriterionStatus(criterion, CriterionState.NotChecked, "No test results are uploaded");
                    }

                    var failing = rows.Where(r => r.FailingTests > 0).Select(r => $"{r.Requirement.Code}: {r.FailingTests} failing").ToList();
                    if (failing.Count > 0)
                    {
                        return new CriterionStatus(criterion, CriterionState.NotMet, string.Join("; ", failing));
                    }

                    return rows.Any(r => r.Evidence.Any(e => !e.IsExpired && e.AcceptedFrom == Domain.Mappings.EvidenceSourceKind.TestRun))
                        ? new CriterionStatus(criterion, CriterionState.Met)
                        : new CriterionStatus(criterion, CriterionState.NotMet, "No passing test run on its requirements");

                case DoneCheck.NoOpenAiSuggestion:
                    var waiting = compliance?.SuggestionsFor(item.Id).Count() ?? 0;
                    return waiting == 0 ? new CriterionStatus(criterion, CriterionState.Met)
                        : new CriterionStatus(criterion, CriterionState.NotMet, $"{waiting} AI {(waiting == 1 ? "suggestion waits" : "suggestions wait")}");

                default:
                    return new CriterionStatus(criterion, CriterionState.NotChecked);
            }
        }

        // R2: the template's sections per kind
        public static IReadOnlyList<string> EmptySections(WorkItem item)
        {
            var empty = new List<string>();
            switch (item.Type)
            {
                case WorkItemType.UseCase:
                    if (string.IsNullOrWhiteSpace(item.UseCase?.PrimaryActor)) { empty.Add("primary actor"); }
                    if (item.UseCase is not { Steps.Count: > 0 }) { empty.Add("main success scenario"); }
                    if (string.IsNullOrWhiteSpace(item.UseCase?.Extensions)) { empty.Add("extensions"); }
                    break;
                case WorkItemType.Requirement:
                    if (string.IsNullOrWhiteSpace(item.AcceptanceCriteria)) { empty.Add("acceptance criteria"); }
                    break;
                default:
                    if (string.IsNullOrWhiteSpace(item.Rules)) { empty.Add("rules"); }
                    if (string.IsNullOrWhiteSpace(item.Examples)) { empty.Add("examples"); }
                    break;
            }

            return empty;
        }

        // R7: one line per finished work item and criterion; the version of the Definition of Done in every line
        public static string Csv(DoneAuditView view, IReadOnlyDictionary<Guid, string> names)
        {
            static string Cell(string? text) => $"\"{(text ?? "").Replace("\"", "\"\"")}\"";
            var csv = new StringBuilder();
            csv.AppendLine("Work item,Kind,Finished on,Meets the Definition of Done,Criterion,Status,Note,Definition of Done version");
            foreach (var row in view.Finished)
            {
                foreach (var criterion in row.Criteria)
                {
                    csv.AppendLine(string.Join(",",
                        Cell(row.Item.Title), Cell(row.Item.Type.ToString()), Cell(row.Item.FinishedAt?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)),
                        Cell(row.Meets ? "Yes" : "No"), Cell(criterion.Criterion.Title), Cell(criterion.State.ToString()), Cell(criterion.Note),
                        Cell(view.Definition.Version.ToString(CultureInfo.InvariantCulture))));
                }
            }

            return csv.ToString();
        }
    }

    // US-109: the Definition of Done, the ticks, "Done" on use cases and requirements, and the audit
    public class DefinitionOfDoneService
    {
        private readonly IProjectRepository _projects;
        private readonly IWorkItemRepository _workItems;
        private readonly IDefinitionOfDoneRepository _definitions;
        private readonly ComplianceQueries _compliance;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public DefinitionOfDoneService(IProjectRepository projects, IWorkItemRepository workItems, IDefinitionOfDoneRepository definitions,
            ComplianceQueries compliance, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _workItems = workItems;
            _definitions = definitions;
            _compliance = compliance;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // R6, R7: everyone in the organisation, the Auditor too; null for another organisation's project. compliance may be
        // passed in by a page that has it already, so it is not read twice.
        public async Task<DoneAuditView?> AuditAsync(Guid projectId, CancellationToken cancellationToken = default, ProjectCompliance? compliance = null)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var definition = await _definitions.GetAsync(project.Id, cancellationToken) ?? DefinitionOfDone.Default(project.Id);
            compliance ??= await _compliance.ForProjectAsync(project.Id, cancellationToken);
            var ticks = await _definitions.ListTicksAsync(project.Id, cancellationToken);
            var names = organization.Users.ToDictionary(u => u.Id, u => u.Name);
            var rows = (compliance?.WorkItems ?? []).Where(w => w.State != WorkItemState.Removed)
                .Select(w => DoneAudit.For(w, definition.For(w.Type), compliance, ticks, names))
                .OrderBy(r => r.Item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new DoneAuditView(project, definition, rows);
        }

        public async Task<string?> CsvAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await AuditAsync(projectId, cancellationToken) is not { } view || await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return null;
            }

            return DoneAudit.Csv(view, organization.Users.ToDictionary(u => u.Id, u => u.Name));
        }

        // R8: DecideCompliance; logged with the old and the new criteria
        public async Task ChangeAsync(Guid projectId, WorkItemType kind, IEnumerable<DoneCheck> checks, string? manual, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var definition = await _definitions.GetAsync(project.Id, cancellationToken);
            var isNew = definition is null;
            definition ??= DefinitionOfDone.Default(project.Id);
            var change = definition.Change(kind, checks, (manual ?? "").Split('\n'), user.Id, user.Role, Now);
            if (change is not { } done)
            {
                return;
            }

            if (isNew)
            {
                await _definitions.AddAsync(definition, cancellationToken);
            }

            await _log.LogAsync(ActivityAction.DefinitionOfDoneChanged, project.Id,
                $"{KindName(kind)}: {done.Before} → {done.After} (version {definition.Version})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R4: LinkWork; who, role and when
        public async Task TickAsync(Guid projectId, Guid workItemId, string criterion, CancellationToken cancellationToken = default)
        {
            var (organization, user, project, item) = await OpenAsync(projectId, workItemId, cancellationToken);
            var definition = await _definitions.GetAsync(project.Id, cancellationToken) ?? DefinitionOfDone.Default(project.Id);
            var manual = definition.For(item.Type).FirstOrDefault(c => c.IsManual && string.Equals(c.Title, (criterion ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException("Only a manual criterion of the Definition of Done can be ticked"); // R3
            if ((await _definitions.ListTicksAsync(project.Id, cancellationToken)).Any(t => t.WorkItemId == item.Id
                    && string.Equals(t.Criterion, manual.Title, StringComparison.OrdinalIgnoreCase) && t.CountsFor(item)))
            {
                throw new DomainException($"\"{manual.Title}\" is already ticked");
            }

            await _definitions.AddTickAsync(DoneTick.Tick(project.Id, item.Id, manual.Title, user.Id, user.Role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.DoneCriterionTicked, project.Id, $"{item.Title}: {manual.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task UntickAsync(Guid projectId, Guid workItemId, string criterion, CancellationToken cancellationToken = default)
        {
            var (_, user, project, item) = await OpenAsync(projectId, workItemId, cancellationToken);
            var tick = (await _definitions.ListTicksAsync(project.Id, cancellationToken)).FirstOrDefault(t => t.WorkItemId == item.Id
                    && string.Equals(t.Criterion, (criterion ?? "").Trim(), StringComparison.OrdinalIgnoreCase) && t.CountsFor(item))
                ?? throw new DomainException("The criterion is not ticked");
            tick.Untick(user.Id, Now);
            await _log.LogAsync(ActivityAction.DoneCriterionTicked, project.Id, $"{item.Title}: {tick.Criterion} unticked", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: a use case or a requirement is finished when a person marks it Done
        public async Task MarkDoneAsync(Guid projectId, Guid workItemId, bool done, CancellationToken cancellationToken = default)
        {
            var (_, user, project, item) = await OpenAsync(projectId, workItemId, cancellationToken);
            if (done)
            {
                item.MarkDone(user.Id, Now);
            }
            else
            {
                item.MarkNotDone();
            }

            await _log.LogAsync(ActivityAction.WorkItemMarkedDone, project.Id, $"{item.Title}{(done ? "" : " is not done after all")}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public static string KindName(WorkItemType kind) => kind switch
        {
            WorkItemType.UseCase => "Use case",
            WorkItemType.Requirement => "Requirement",
            _ => "User story"
        };

        private async Task<(Domain.Organizations.Organization Organization, User User, Project Project, WorkItem Item)> OpenAsync(
            Guid projectId, Guid workItemId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var item = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == workItemId)
                ?? throw new DomainException("Work item not found");
            return (organization, user, project, item);
        }
    }
}
