using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-155 R3: how urgent the AI thinks the missing work is
    public enum NeedsActionPriority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    // US-155: what happened to a "needs action" mark
    public enum NeedsActionStatus
    {
        // Marked by the AI; nobody has decided yet
        Pending,

        // A person agreed and asked for a story (R5); the mark stays until a story covers the requirement
        Confirmed,

        // A person said it is not needed (R6); the requirement became not applicable
        Dismissed,

        // US-156: a newer assessment replaced it before anyone decided
        Replaced
    }

    // US-155: the AI's judgement that a requirement applies to the product and has no work yet ("Needs action"), with its
    // reason, a priority and the context it used. Only a suggestion: a person confirms or dismisses it (R3, R7).
    // A requirement that gets covered is no longer shown as needing action, whatever its status (R5).
    public class NeedsActionSuggestion
    {
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid FrameworkId { get; private set; }
        public Guid RequirementId { get; private set; }

        public string Reason { get; private set; } = "";
        public NeedsActionPriority Priority { get; private set; }

        // R4: raised to High because a measure of an open High or Critical risk names the requirement
        public bool RaisedByRisk { get; private set; }

        // e.g. "purpose, scope, classification"
        public string ContextUsed { get; private set; } = "";

        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public NeedsActionStatus Status { get; private set; } = NeedsActionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        // R6: why it is not needed
        public string? DecisionReason { get; private set; }

        // US-156 R3: the product changed after a person said "not needed"; the decision is kept, to be looked at again
        public DateTime? ContextChangedAt { get; private set; }

        public bool IsPending => Status == NeedsActionStatus.Pending;

        // Shown as "Needs action" while nobody dismissed it and no newer assessment replaced it
        public bool IsOpen => Status is NeedsActionStatus.Pending or NeedsActionStatus.Confirmed;

        // For EF Core only
        private NeedsActionSuggestion() { }

        public static NeedsActionSuggestion Create(
            Guid projectId, Guid frameworkId, Guid requirementId, string reason, NeedsActionPriority priority, bool highRisk,
            string contextUsed, string model, string promptVersion, DateTime at)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("The AI gave no reason");
            }

            if (!Enum.IsDefined(priority))
            {
                throw new DomainException("Unknown priority");
            }

            return new NeedsActionSuggestion
            {
                ProjectId = projectId,
                FrameworkId = frameworkId,
                RequirementId = requirementId,
                Reason = text.Length <= MaxReasonLength ? text : text[..MaxReasonLength],
                // R4: an open High or Critical risk makes it High, whatever the AI said
                Priority = highRisk ? NeedsActionPriority.High : priority,
                RaisedByRisk = highRisk && priority != NeedsActionPriority.High,
                ContextUsed = contextUsed,
                Model = model,
                PromptVersion = promptVersion,
                CreatedAt = at
            };
        }

        // R5: the person agrees; a story draft is asked for
        public void Confirm(Guid userId, DateTime at, UserRole? role)
            => Decide(NeedsActionStatus.Confirmed, userId, at, role, null, IsPending);

        // R6: not needed, with a reason; the caller makes the requirement not applicable
        public void Dismiss(string reason, Guid userId, DateTime at, UserRole? role)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Say why it is not needed");
            }

            // Also after "Create story", when the draft showed it is not needed after all
            Decide(NeedsActionStatus.Dismissed, userId, at, role, text.Length <= MaxReasonLength ? text : text[..MaxReasonLength], IsOpen);
        }

        // US-156 R3: a dismissed mark is never changed by a new assessment, only marked "Based on an earlier description"
        public void MarkContextChanged(DateTime at)
        {
            if (Status == NeedsActionStatus.Dismissed)
            {
                ContextChangedAt = at;
            }
        }

        // US-156: a newer assessment replaces a mark nobody has decided on
        public void Replace()
        {
            if (IsPending)
            {
                Status = NeedsActionStatus.Replaced;
            }
        }

        private void Decide(NeedsActionStatus status, Guid userId, DateTime at, UserRole? role, string? reason, bool allowed)
        {
            if (!allowed)
            {
                throw new DomainException("The mark has already been decided");
            }

            Status = status;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
            DecisionReason = reason;
        }
    }
}
