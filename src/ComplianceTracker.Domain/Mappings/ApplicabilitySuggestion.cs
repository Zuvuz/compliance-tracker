using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-060 R6: an AI suggestion that a requirement does not apply to a project, with the AI's reason and the context it
    // used. Only a suggestion: the requirement becomes not applicable when a person with DecideCompliance accepts it.
    // US-061 R2, R3: ClassifiedAt is the classification it relied on, so a new classification can replace it.
    public class ApplicabilitySuggestion
    {
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid FrameworkId { get; private set; }
        public Guid RequirementId { get; private set; }

        public string Reason { get; private set; } = "";

        // e.g. "purpose, scope, classification"
        public string ContextUsed { get; private set; } = "";

        public DateTime? ClassifiedAt { get; private set; }

        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private ApplicabilitySuggestion() { }

        public static ApplicabilitySuggestion Create(
            Guid projectId, Guid frameworkId, Guid requirementId, string reason, string contextUsed, DateTime? classifiedAt,
            string model, string promptVersion, DateTime at)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("The AI gave no reason");
            }

            return new ApplicabilitySuggestion
            {
                ProjectId = projectId,
                FrameworkId = frameworkId,
                RequirementId = requirementId,
                Reason = text.Length <= MaxReasonLength ? text : text[..MaxReasonLength],
                ContextUsed = contextUsed,
                ClassifiedAt = classifiedAt,
                Model = model,
                PromptVersion = promptVersion,
                CreatedAt = at
            };
        }

        // R6: the caller marks the requirement not applicable with the reason the person chose
        public void Accept(Guid userId, DateTime at, UserRole? role)
            => Decide(SuggestionStatus.Approved, userId, at, role);

        public void Reject(Guid userId, DateTime at, UserRole? role)
            => Decide(SuggestionStatus.Rejected, userId, at, role);

        // US-061 R3: the classification changed; the suggestion is kept, marked as replaced
        public void Replace()
        {
            if (IsPending)
            {
                Status = SuggestionStatus.Replaced;
            }
        }

        private void Decide(SuggestionStatus status, Guid userId, DateTime at, UserRole? role)
        {
            if (!IsPending)
            {
                throw new DomainException("The suggestion has already been decided");
            }

            Status = status;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }
    }
}
