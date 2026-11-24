using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    // US-212 R1: where the nonconformity came from
    public enum NonconformitySource
    {
        InternalAudit,
        ExternalAudit,
        SecurityEvent,
        MissedDeadline,
        Complaint,
        ManagementReview,
        Other
    }

    public enum NonconformitySeverity
    {
        Major,
        Minor,
        Observation
    }

    // US-212: Open → ActionPlanned → ActionDone → Effective (closed). R4: "not effective" goes back to Open.
    public enum NonconformityStatus
    {
        Open,
        ActionPlanned,
        ActionDone,
        Effective
    }

    // US-212 R4: a corrective action that did not work, kept so the history shows what was tried
    public sealed record FailedAction(string Action, DateOnly CheckedOn, string Result);

    // US-212: one nonconformity in the organisation's register (ISO/IEC 27001 10.2): what happened, the correction done at
    // once, the cause, the corrective action with its owner and deadline, and the check that the action worked.
    public class Nonconformity
    {
        public const int MaxTitleLength = 200;
        public const int MaxTextLength = 2000;
        public const string CloseFirst = "Record the cause, the corrective action and the effectiveness check first";

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Title { get; private set; } = "";
        public string? Description { get; private set; }
        public NonconformitySource Source { get; private set; }

        // R6: the audit finding or security event it came from, if any
        public Guid? SourceId { get; private set; }
        public NonconformitySeverity Severity { get; private set; }

        public string? Correction { get; private set; }
        public string? Cause { get; private set; }

        public string? Action { get; private set; }
        public Guid? ActionOwnerId { get; private set; }
        public DateOnly? ActionDueOn { get; private set; }
        public DateTime? ActionDoneAt { get; private set; }

        public DateOnly? CheckedOn { get; private set; }
        public Guid? CheckedById { get; private set; }
        public string? CheckResult { get; private set; }

        public NonconformityStatus Status { get; private set; } = NonconformityStatus.Open;

        public List<FailedAction> FailedActions { get; private set; } = [];

        public Guid RaisedById { get; private set; }
        public DateTime RaisedAt { get; private set; }
        public DateTime? ClosedAt { get; private set; }

        public bool IsClosed => Status == NonconformityStatus.Effective;

        // For EF Core only
        private Nonconformity() { }

        // R1
        public static Nonconformity Raise(Guid organizationId, string? title, string? description, NonconformitySource? source, Guid? sourceId,
            NonconformitySeverity? severity, Guid raisedById, DateTime at)
        {
            var cleanTitle = (title ?? "").Trim();
            if (cleanTitle.Length == 0)
            {
                throw new DomainException("Give the nonconformity a title");
            }

            if (cleanTitle.Length > MaxTitleLength)
            {
                throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
            }

            if (source is null || !Enum.IsDefined(source.Value))
            {
                throw new DomainException("Choose where the nonconformity came from");
            }

            if (severity is null || !Enum.IsDefined(severity.Value))
            {
                throw new DomainException("Choose how severe the nonconformity is");
            }

            return new Nonconformity
            {
                OrganizationId = organizationId,
                Title = cleanTitle,
                Description = Text(description, "The description"),
                Source = source.Value,
                SourceId = sourceId == Guid.Empty ? null : sourceId,
                Severity = severity.Value,
                RaisedById = raisedById,
                RaisedAt = at
            };
        }

        // 10.2 a) and b): the correction done at once, and the cause
        public IReadOnlyList<string> Analyse(string? correction, string? cause)
        {
            RequireOpen();
            var changes = new List<string>();
            var cleanCorrection = Text(correction, "The correction");
            var cleanCause = Text(cause, "The cause");
            if (cleanCorrection != Correction) { changes.Add("correction"); }
            if (cleanCause != Cause) { changes.Add("cause"); }
            Correction = cleanCorrection;
            Cause = cleanCause;
            return changes;
        }

        // 10.2 c): the corrective action, with an owner and a deadline that has not passed
        public void PlanAction(string? action, Guid? ownerId, DateOnly? dueOn, DateOnly today)
        {
            RequireOpen();
            var clean = Text(action, "The corrective action") ?? throw new DomainException("Describe the corrective action");
            if (ownerId is null || ownerId == Guid.Empty)
            {
                throw new DomainException("Choose who owns the corrective action");
            }

            if (dueOn is null)
            {
                throw new DomainException("Give the corrective action a deadline");
            }

            if (dueOn < today)
            {
                throw new DomainException("The deadline has passed; choose a later date");
            }

            Action = clean;
            ActionOwnerId = ownerId;
            ActionDueOn = dueOn;
            ActionDoneAt = null;
            Status = NonconformityStatus.ActionPlanned;
        }

        public void MarkActionDone(DateTime at)
        {
            RequireOpen();
            if (Status != NonconformityStatus.ActionPlanned)
            {
                throw new DomainException(Status == NonconformityStatus.ActionDone
                    ? "The corrective action is done already"
                    : "Plan the corrective action first");
            }

            ActionDoneAt = at;
            Status = NonconformityStatus.ActionDone;
        }

        // R3, R4, 10.2 d): the check that the action worked. Effective closes the nonconformity; not effective opens it
        // again, keeps the failed action and asks for a new one. Returns true when it closed.
        public bool Check(string? result, bool effective, DateOnly checkedOn, Guid checkedById, DateTime at)
        {
            RequireOpen();
            if (Status != NonconformityStatus.ActionDone)
            {
                throw new DomainException("Mark the corrective action done before checking whether it worked");
            }

            var clean = Text(result, "The result") ?? throw new DomainException("Write what the check found");
            if (effective)
            {
                if (Cause is null)
                {
                    throw new DomainException(CloseFirst);
                }

                CheckedOn = checkedOn;
                CheckedById = checkedById;
                CheckResult = clean;
                Status = NonconformityStatus.Effective;
                ClosedAt = at;
                return true;
            }

            FailedActions = [.. FailedActions, new FailedAction(Action!, checkedOn, clean)];
            Action = null;
            ActionOwnerId = null;
            ActionDueOn = null;
            ActionDoneAt = null;
            Status = NonconformityStatus.Open;
            return false;
        }

        // R2: closing needs the cause, the corrective action and an effectiveness check that found it worked
        public void Close()
        {
            if (!IsClosed)
            {
                throw new DomainException(CloseFirst);
            }
        }

        // A closed nonconformity that comes back: open again with the reason; the old action and check are kept as failed
        public void Reopen(string? reason, DateOnly on)
        {
            if (!IsClosed)
            {
                throw new DomainException("Only a closed nonconformity can be opened again");
            }

            var clean = Text(reason, "The reason") ?? throw new DomainException("Give the reason it is opened again");
            FailedActions = [.. FailedActions, new FailedAction(Action!, on, $"Opened again: {clean}")];
            Action = null;
            ActionOwnerId = null;
            ActionDueOn = null;
            ActionDoneAt = null;
            CheckedOn = null;
            CheckedById = null;
            CheckResult = null;
            ClosedAt = null;
            Status = NonconformityStatus.Open;
        }

        // R5
        public bool IsOverdue(DateOnly today) => Status == NonconformityStatus.ActionPlanned && ActionDueOn < today;

        private void RequireOpen()
        {
            if (IsClosed)
            {
                throw new DomainException("The nonconformity is closed; open it again first");
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
}
