using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    public enum TaskInterval
    {
        Monthly,
        Quarterly,
        HalfYearly,
        Yearly
    }

    // US-215: a task in the ISMS yearly plan that comes back at its interval (ISO/IEC 27001 8.1), with the controls it shows
    // and the evidence it needs. Done tasks keep their completions; the evidence is an organisation document linked to the
    // controls (US-136), so every project that follows them gets it.
    public class RecurringTask
    {
        public const int MaxTitleLength = 200;
        public const int MaxTextLength = 1000;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Title { get; private set; } = "";
        public string? Description { get; private set; }
        public TaskInterval Interval { get; private set; }
        public Guid OwnerId { get; private set; }
        public DateOnly NextDueOn { get; private set; }
        public List<string> Controls { get; private set; } = [];

        // R3: what the evidence is, e.g. "restore log"; null when none is needed
        public string? EvidenceNeeded { get; private set; }

        // R6: the suggestion it was accepted from, so it is not suggested again
        public string? SuggestionKey { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        private readonly List<TaskCompletion> _completions = [];
        public IReadOnlyList<TaskCompletion> Completions => _completions.OrderByDescending(c => c.DoneOn).ThenByDescending(c => c.At).ToList();

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private RecurringTask() { }

        // R1
        public static RecurringTask Create(Guid organizationId, string? title, string? description, TaskInterval? interval, Guid? ownerId, DateOnly? firstDueOn,
            IEnumerable<string?>? controls, string? evidenceNeeded, string? suggestionKey, DateTime at)
        {
            var task = new RecurringTask { OrganizationId = organizationId, SuggestionKey = suggestionKey, CreatedAt = at };
            task.Change(title, description, interval, ownerId, firstDueOn, controls, evidenceNeeded);
            return task;
        }

        public void Change(string? title, string? description, TaskInterval? interval, Guid? ownerId, DateOnly? nextDueOn, IEnumerable<string?>? controls,
            string? evidenceNeeded)
        {
            RequireActive();
            var cleanTitle = (title ?? "").Trim();
            if (cleanTitle.Length == 0)
            {
                throw new DomainException("Give the task a title");
            }

            if (cleanTitle.Length > MaxTitleLength)
            {
                throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
            }

            if (interval is null || !Enum.IsDefined(interval.Value))
            {
                throw new DomainException("Choose how often the task comes back");
            }

            if (ownerId is null || ownerId == Guid.Empty)
            {
                throw new DomainException("Choose the owner of the task");
            }

            if (nextDueOn is null)
            {
                throw new DomainException("Give the task its next deadline");
            }

            var cleanControls = new List<string>();
            foreach (var control in (controls ?? []).Select(c => (c ?? "").Trim()).Where(c => c.Length > 0))
            {
                var code = AuditProgramme.ControlCode(control) ?? (AuditProgramme.Clauses.Any(c => control == c || control.StartsWith(c + ".", StringComparison.Ordinal))
                    ? control
                    : throw new DomainException($"{control} is not a clause or an Annex A control"));
                if (!cleanControls.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    cleanControls.Add(code);
                }
            }

            Title = cleanTitle;
            Description = Text(description, "The description");
            Interval = interval.Value;
            OwnerId = ownerId.Value;
            NextDueOn = nextDueOn.Value;
            Controls = cleanControls;
            EvidenceNeeded = Text(evidenceNeeded, "The evidence");
        }

        // R2, R3: done on a day; the next deadline is the old deadline plus the interval, not the day it was done
        public TaskCompletion Complete(DateOnly? doneOn, Guid byId, string? note, Guid? evidenceDocumentId, DateOnly today, DateTime at)
        {
            RequireActive();
            if (doneOn is null)
            {
                throw new DomainException("Give the day the task was done");
            }

            if (doneOn > today)
            {
                throw new DomainException("The day it was done cannot be in the future");
            }

            if (EvidenceNeeded is not null && evidenceDocumentId is null)
            {
                throw new DomainException($"{Title} needs evidence: {EvidenceNeeded}");
            }

            var completion = new TaskCompletion(Id, NextDueOn, doneOn.Value, byId, Text(note, "The note"), evidenceDocumentId, at);
            _completions.Add(completion);
            NextDueOn = Next(NextDueOn, Interval);
            return completion;
        }

        // R5
        public bool IsOverdue(DateOnly today) => IsActive && NextDueOn < today;

        // The deadlines from a day for the next months, for the year wheel
        public IReadOnlyList<DateOnly> DeadlinesUntil(DateOnly until)
        {
            var deadlines = new List<DateOnly>();
            for (var due = NextDueOn; due <= until && deadlines.Count < 24; due = Next(due, Interval))
            {
                deadlines.Add(due);
            }

            return deadlines;
        }

        public void Remove(DateTime at)
        {
            RequireActive();
            RemovedAt = at;
        }

        // R2: the last day of a month stays the last day, e.g. 2026-12-31 + a quarter = 2027-03-31
        public static DateOnly Next(DateOnly due, TaskInterval interval)
        {
            var months = interval switch
            {
                TaskInterval.Monthly => 1,
                TaskInterval.Quarterly => 3,
                TaskInterval.HalfYearly => 6,
                _ => 12
            };
            var next = due.AddMonths(months);
            var lastDay = due.Day == DateTime.DaysInMonth(due.Year, due.Month);
            return lastDay ? new DateOnly(next.Year, next.Month, DateTime.DaysInMonth(next.Year, next.Month)) : next;
        }

        private void RequireActive()
        {
            if (!IsActive)
            {
                throw new DomainException("The task has been removed");
            }
        }

        private static string? Text(string? value, string what)
        {
            var clean = (value ?? "").Trim();
            if (clean.Length > MaxTextLength)
            {
                throw new DomainException($"{what} must be {MaxTextLength} characters or fewer");
            }

            return clean.Length == 0 ? null : clean;
        }
    }

    // US-215: one time a recurring task was done, for the deadline it met
    public class TaskCompletion
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid TaskId { get; private set; }
        public DateOnly DueOn { get; private set; }
        public DateOnly DoneOn { get; private set; }
        public Guid ById { get; private set; }
        public string? Note { get; private set; }
        public Guid? EvidenceDocumentId { get; private set; }
        public DateTime At { get; private set; }

        public bool WasLate => DoneOn > DueOn;

        // For EF Core only
        private TaskCompletion() { }

        internal TaskCompletion(Guid taskId, DateOnly dueOn, DateOnly doneOn, Guid byId, string? note, Guid? evidenceDocumentId, DateTime at)
        {
            TaskId = taskId;
            DueOn = dueOn;
            DoneOn = doneOn;
            ById = byId;
            Note = note;
            EvidenceDocumentId = evidenceDocumentId;
            At = at;
        }
    }

    // US-215 R6: a task the starting plan suggests, from a control in use in the SoA or always (Trigger null)
    public sealed record TaskSuggestion(string Key, string Title, TaskInterval Interval, IReadOnlyList<string> Controls, string? Evidence, string? Trigger)
    {
        public static readonly IReadOnlyList<TaskSuggestion> All =
        [
            new("access-review", "Access review", TaskInterval.Quarterly, ["A.5.18"], "access review list", "A.5.18"),
            new("restore-test", "Backup restore test", TaskInterval.Quarterly, ["A.8.13"], "restore log", "A.8.13"),
            new("policy-review", "Review of the policies", TaskInterval.Yearly, ["A.5.1"], null, "A.5.1"),
            new("vulnerability-review", "Review of the vulnerability scans", TaskInterval.Monthly, ["A.8.8"], "scan report", "A.8.8"),
            new("incident-exercise", "Incident response exercise", TaskInterval.Yearly, ["A.5.24", "A.5.26"], "exercise report", "A.5.24"),
            new("awareness-training", "Security awareness training", TaskInterval.Yearly, ["A.6.3"], null, "A.6.3"),
            new("supplier-review", "Supplier review", TaskInterval.Yearly, ["A.5.19", "A.5.22"], null, "A.5.19"),
            new("risk-review", "Review of the risk assessment", TaskInterval.Yearly, ["6.1"], null, null),
            new("internal-audit", "Internal audit", TaskInterval.Yearly, ["9.2"], null, null),
            new("management-review", "Management review", TaskInterval.Yearly, ["9.3"], null, null)
        ];

        // R6: the always ones and those whose control is in use, minus the ones already in the plan
        public static IReadOnlyList<TaskSuggestion> For(IEnumerable<string> applicableControls, IEnumerable<string?> accepted)
        {
            var controls = applicableControls.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var taken = accepted.OfType<string>().ToHashSet();
            return All.Where(s => !taken.Contains(s.Key) && (s.Trigger is null || controls.Contains(s.Trigger))).ToList();
        }
    }
}
