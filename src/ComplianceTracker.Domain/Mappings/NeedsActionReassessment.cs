namespace ComplianceTracker.Domain.Mappings
{
    // US-156: a new assessment of what needs action (US-155), asked for when the product changes. It waits a few minutes,
    // so several changes in a row give one assessment (R2), and it is run in the background, never during the request.
    public class NeedsActionReassessment
    {
        // R2: changes within this time give one assessment
        public static readonly TimeSpan Delay = TimeSpan.FromMinutes(5);
        public const int MaxReasonsLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid ProjectId { get; private set; }

        // R6: what started it, e.g. "intended purpose changed; CRA classification changed"
        public string Reasons { get; private set; } = "";

        public DateTime RequestedAt { get; private set; }
        public DateTime DueAt { get; private set; }
        public DateTime? DoneAt { get; private set; }

        // R4: the AI could not be reached; it is tried again later
        public bool WaitingForAi { get; private set; }

        // e.g. "2 need action, 1 does not apply"
        public string? Result { get; private set; }

        public bool IsDone => DoneAt is not null;

        // For EF Core only
        private NeedsActionReassessment() { }

        public static NeedsActionReassessment Request(Guid organizationId, Guid projectId, string reason, DateTime at)
            => new()
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Reasons = Clean(reason),
                RequestedAt = at,
                DueAt = at + Delay
            };

        // R2: another change before it ran: one assessment, later, with both reasons
        public void Again(string reason, DateTime at)
        {
            if (IsDone)
            {
                throw new InvalidOperationException("A finished assessment cannot be asked again; request a new one");
            }

            var cleaned = Clean(reason);
            var reasons = Reasons.Split("; ", StringSplitOptions.RemoveEmptyEntries).ToList();
            if (cleaned.Length > 0 && !reasons.Contains(cleaned))
            {
                reasons.Add(cleaned);
            }

            var joined = string.Join("; ", reasons);
            Reasons = joined.Length <= MaxReasonsLength ? joined : joined[..MaxReasonsLength];
            DueAt = at + Delay;
        }

        public bool IsDue(DateTime now) => !IsDone && DueAt <= now;

        // R4
        public void WaitForAi() => WaitingForAi = true;

        public void Finish(string result, DateTime at)
        {
            Result = result;
            WaitingForAi = false;
            DoneAt = at;
        }

        private static string Clean(string? reason)
        {
            var text = (reason ?? "").Trim();
            return text.Length <= MaxReasonsLength ? text : text[..MaxReasonsLength];
        }
    }
}
