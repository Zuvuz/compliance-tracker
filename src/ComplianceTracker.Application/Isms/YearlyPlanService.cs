using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-215: storage for the recurring tasks (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IRecurringTaskRepository
    {
        // The organisation's tasks with their completions, removed ones too
        Task<IReadOnlyList<RecurringTask>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(RecurringTask task, CancellationToken cancellationToken = default);
    }

    // A task as typed on the page
    public sealed record RecurringTaskInput(string? Title, string? Description, TaskInterval? Interval, Guid? OwnerId, DateOnly? NextDueOn, string? Controls,
        string? EvidenceNeeded);

    // One month of the year wheel and the deadlines in it
    public sealed record PlanMonth(DateOnly Month, IReadOnlyList<(RecurringTask Task, DateOnly DueOn)> Deadlines);

    public sealed record YearlyPlanView(
        IReadOnlyList<RecurringTask> Tasks,
        IReadOnlyList<TaskSuggestion> Suggestions,
        IReadOnlyList<PlanMonth> Months,
        IReadOnlyDictionary<Guid, string> Members,
        bool CanChange,
        DateOnly Today)
    {
        public string NameOf(Guid id) => Members.GetValueOrDefault(id) ?? "Former member";
    }

    public class YearlyPlanService
    {
        private readonly IRecurringTaskRepository _tasks;
        private readonly IOrganisationDocumentRepository _documents;
        private readonly OrganisationDocumentService _documentService;
        private readonly IFrameworkRepository _frameworks;
        private readonly INonconformityRepository _nonconformities;
        private readonly StatementOfApplicabilityQueries _soa;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public YearlyPlanService(IRecurringTaskRepository tasks, IOrganisationDocumentRepository documents, OrganisationDocumentService documentService,
            IFrameworkRepository frameworks, INonconformityRepository nonconformities, StatementOfApplicabilityQueries soa, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _tasks = tasks;
            _documents = documents;
            _documentService = documentService;
            _frameworks = frameworks;
            _nonconformities = nonconformities;
            _soa = soa;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member reads the plan: the overdue first, then by deadline
        public async Task<YearlyPlanView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var all = await _tasks.ListAsync(organization.Id, cancellationToken);
            var tasks = all.Where(t => t.IsActive).OrderBy(t => t.NextDueOn).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();

            // R6: suggested from the controls in use in the SoA (US-053); the clause tasks always
            var applicable = await _soa.ForOrganizationAsync(cancellationToken) is { } soa
                ? StatementOfApplicability.Controls(soa.Projects).Where(c => c.Applicable).Select(c => c.Code).ToList()
                : [];
            return new YearlyPlanView(tasks, TaskSuggestion.For(applicable, all.Select(t => t.SuggestionKey)), Months(tasks, Today),
                organization.Users.ToDictionary(u => u.Id, u => u.Name), await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // The year wheel: this month and the next eleven, with every deadline in them
        public static IReadOnlyList<PlanMonth> Months(IEnumerable<RecurringTask> tasks, DateOnly today)
        {
            var first = new DateOnly(today.Year, today.Month, 1);
            var last = first.AddMonths(12).AddDays(-1);
            var deadlines = tasks.SelectMany(t => t.DeadlinesUntil(last).Select(d => (Task: t, DueOn: d))).ToList();
            return Enumerable.Range(0, 12).Select(i =>
            {
                var month = first.AddMonths(i);
                return new PlanMonth(month, deadlines
                    .Where(d => (d.DueOn.Year == month.Year && d.DueOn.Month == month.Month) || (i == 0 && d.DueOn < first))
                    .OrderBy(d => d.DueOn).ToList());
            }).ToList();
        }

        // R1
        public async Task<RecurringTask> AddAsync(RecurringTaskInput input, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            RequireMember(organization, input.OwnerId);
            var task = RecurringTask.Create(organization.Id, input.Title, input.Description, input.Interval, input.OwnerId, input.NextDueOn,
                AuditProgrammeService.Split(input.Controls), input.EvidenceNeeded, null, Now);
            await _tasks.AddAsync(task, cancellationToken);
            await _log.LogAsync(ActivityAction.RecurringTaskChanged, organization.Id, $"Added {task.Title} ({task.Interval}, first due {task.NextDueOn:dd-MM-yyyy})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return task;
        }

        // R6: nothing is in the plan before the person accepts the suggestion, with its owner and first deadline
        public async Task<RecurringTask> AcceptSuggestionAsync(string? key, Guid? ownerId, DateOnly? firstDueOn, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var suggestion = TaskSuggestion.All.FirstOrDefault(s => s.Key == key) ?? throw new DomainException("Choose a suggestion from the list");
            if ((await _tasks.ListAsync(organization.Id, cancellationToken)).Any(t => t.IsActive && t.SuggestionKey == suggestion.Key))
            {
                throw new DomainException($"{suggestion.Title} is in the plan already");
            }

            RequireMember(organization, ownerId);
            var task = RecurringTask.Create(organization.Id, suggestion.Title, null, suggestion.Interval, ownerId, firstDueOn, suggestion.Controls,
                suggestion.Evidence, suggestion.Key, Now);
            await _tasks.AddAsync(task, cancellationToken);
            await _log.LogAsync(ActivityAction.RecurringTaskChanged, organization.Id, $"Added {task.Title} from the starting plan, first due {task.NextDueOn:dd-MM-yyyy}",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return task;
        }

        public async Task RemoveAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            var (organization, task) = await TaskAsync(taskId, cancellationToken);
            task.Remove(Now);
            await _log.LogAsync(ActivityAction.RecurringTaskChanged, organization.Id, $"Removed {task.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2, R3, R4: the evidence file becomes an organisation document linked to the task's controls in the organisation's
        // ISO/IEC 27001, so the projects that follow them get it as evidence (US-136, US-057)
        public async Task<TaskCompletion> CompleteAsync(Guid taskId, DateOnly? doneOn, string? note, string? fileName, byte[]? content,
            CancellationToken cancellationToken = default)
        {
            var (organization, task) = await TaskAsync(taskId, cancellationToken);
            var (_, user) = await _member.RequireAsync(cancellationToken);
            OrganisationDocument? document = null;
            if (content is { Length: > 0 } && !string.IsNullOrWhiteSpace(fileName))
            {
                var file = EvidenceFile.Create(fileName, content);
                var day = doneOn ?? Today;
                document = OrganisationDocument.Create(organization.Id, OrganisationDocumentKind.Other, $"{task.Title} {day:yyyy-MM-dd}", user.Id,
                    day.ToString("yyyy-MM-dd"), file, null, user.Id, Now);
                if ((await _frameworks.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(StatementOfApplicability.IsIso27001) is { } iso)
                {
                    foreach (var control in task.Controls)
                    {
                        document.Link(iso.Id, control, user.Id, Now);
                    }
                }

                await _documents.AddAsync(document, file.Content, cancellationToken);
            }

            var completion = task.Complete(doneOn, user.Id, note, document?.Id, Today, Now);
            if (document is not null)
            {
                await _documentService.SyncAsync(organization.Id, cancellationToken);
            }

            await _log.LogAsync(ActivityAction.RecurringTaskChanged, organization.Id,
                $"{task.Title} done {completion.DoneOn:dd-MM-yyyy}{(document is not null ? $" with {document.FileName}" : "")}; next due {task.NextDueOn:dd-MM-yyyy}",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return completion;
        }

        // R5: an overdue task becomes a nonconformity (US-212), once while it is open
        public async Task<Nonconformity> RaiseNonconformityAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            var (organization, task) = await TaskAsync(taskId, cancellationToken);
            var (_, user) = await _member.RequireAsync(cancellationToken);
            if (!task.IsOverdue(Today))
            {
                throw new DomainException($"{task.Title} is not overdue");
            }

            if ((await _nonconformities.ListAsync(organization.Id, cancellationToken)).Any(n => !n.IsClosed && n.SourceId == task.Id))
            {
                throw new DomainException($"{task.Title} already has an open nonconformity");
            }

            var nonconformity = Nonconformity.Raise(organization.Id, $"{task.Title} not done by {task.NextDueOn:dd-MM-yyyy}", null,
                NonconformitySource.MissedDeadline, task.Id, NonconformitySeverity.Minor, user.Id, Now);
            await _nonconformities.AddAsync(nonconformity, cancellationToken);
            await _log.LogAsync(ActivityAction.NonconformityChanged, organization.Id, $"Raised {nonconformity.Title} (Minor, from Missed deadline)", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return nonconformity;
        }

        private async Task<(Organization, RecurringTask)> TaskAsync(Guid taskId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var task = (await _tasks.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(t => t.Id == taskId && t.IsActive)
                       ?? throw new DomainException("Task not found");
            return (organization, task);
        }

        private static void RequireMember(Organization organization, Guid? ownerId)
        {
            if (ownerId is { } id && organization.Members.All(m => m.Id != id))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }
        }
    }
}
