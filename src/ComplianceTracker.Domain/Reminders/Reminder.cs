using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Reminders
{
    // US-183 R1: what a reminder is about
    public enum ReminderKind
    {
        EvidenceExpires,
        FindingDue,
        ApprovalWaiting,
        ReportingDeadline,
        SupplierEvidenceExpires
    }

    // US-183: one reminder to one person about one deadline. R2: kept, so the same deadline gives one mail; a changed
    // deadline (another Due) is a new reminder. R4: a mail that could not be sent is tried again a few times.
    public class Reminder
    {
        public const int MaxAttempts = 5;
        public const int MaxErrorLength = 500;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid UserId { get; private set; }
        public ReminderKind Kind { get; private set; }

        // What it is about, e.g. the evidence, the finding or the security event (with the report in Kind's text)
        public Guid SubjectId { get; private set; }

        // The deadline, in UTC
        public DateTime Due { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? SentAt { get; private set; }
        public int Attempts { get; private set; }
        public string? LastError { get; private set; }

        public bool IsSent => SentAt is not null;

        // R4: tried again until it is sent or has failed MaxAttempts times
        public bool ShouldTry => !IsSent && Attempts < MaxAttempts;

        // For EF Core only
        private Reminder() { }

        public static Reminder Create(Guid organizationId, Guid userId, ReminderKind kind, Guid subjectId, DateTime due, DateTime at)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new DomainException("Unknown kind of reminder");
            }

            return new Reminder
            {
                OrganizationId = organizationId,
                UserId = userId,
                Kind = kind,
                SubjectId = subjectId,
                Due = DateTime.SpecifyKind(due, DateTimeKind.Utc),
                CreatedAt = at
            };
        }

        public bool IsFor(Guid userId, ReminderKind kind, Guid subjectId, DateTime due)
            => UserId == userId && Kind == kind && SubjectId == subjectId && Due == DateTime.SpecifyKind(due, DateTimeKind.Utc);

        public void Sent(DateTime at)
        {
            SentAt = at;
            Attempts++;
            LastError = null;
        }

        public void Failed(string error)
        {
            Attempts++;
            LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
        }
    }

    // US-183: which kinds a person may turn off (R5: not the reporting deadlines), and their text in mails and on the page
    public static class ReminderKinds
    {
        public const int DefaultDaysBefore = 30;
        public const int MaxDaysBefore = 90;

        public static bool CanTurnOff(ReminderKind kind) => kind != ReminderKind.ReportingDeadline;

        public static string Title(ReminderKind kind) => kind switch
        {
            ReminderKind.EvidenceExpires => "Evidence that expires",
            ReminderKind.FindingDue => "Audit findings that are due",
            ReminderKind.ApprovalWaiting => "Approvals that wait for me",
            ReminderKind.ReportingDeadline => "CRA and NIS2 reporting deadlines",
            ReminderKind.SupplierEvidenceExpires => "Supplier evidence that expires",
            _ => kind.ToString()
        };

        // R3: the subject names the kind and the deadline, never a title or any content
        public static string Subject(ReminderKind kind, DateTime due) => kind switch
        {
            ReminderKind.EvidenceExpires => $"Reminder: evidence expires on {due:dd-MM-yyyy}",
            ReminderKind.FindingDue => $"Reminder: an audit finding is due on {due:dd-MM-yyyy}",
            ReminderKind.ApprovalWaiting => "Reminder: an approval waits for you",
            ReminderKind.ReportingDeadline => $"Deadline: a security report is due {due:dd-MM-yyyy HH:mm} UTC",
            ReminderKind.SupplierEvidenceExpires => $"Reminder: a supplier's evidence expires on {due:dd-MM-yyyy}",
            _ => "Reminder"
        };
    }
}
