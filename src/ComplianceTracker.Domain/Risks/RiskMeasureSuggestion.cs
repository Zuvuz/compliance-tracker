using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Risks
{
    // US-106 R12: the AI suggests a requirement or a user story that could reduce a risk. Approving adds it as a measure
    // (US-042, US-124); rejecting needs a reason, and the pair is not suggested again.
    public class RiskMeasureSuggestion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RiskId { get; private set; }
        public Guid? RequirementId { get; private set; }
        public Guid? WorkItemId { get; private set; }
        public double Score { get; private set; }
        public string EmbeddingModel { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public RejectionReason? RejectionReason { get; private set; }
        public string? Comment { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private RiskMeasureSuggestion() { }

        // Either a requirement or a story, never both
        public static RiskMeasureSuggestion Create(Guid projectId, Guid riskId, Guid? requirementId, Guid? workItemId, double score, string model, DateTime at)
        {
            if (requirementId is null == workItemId is null)
            {
                throw new DomainException("A measure is a requirement or a user story");
            }

            return new RiskMeasureSuggestion
            {
                ProjectId = projectId,
                RiskId = riskId,
                RequirementId = requirementId,
                WorkItemId = workItemId,
                Score = double.IsNaN(score) ? 0 : Math.Round(Math.Clamp(score, 0, 1), 4),
                EmbeddingModel = model,
                CreatedAt = at
            };
        }

        public bool IsFor(Guid? requirementId, Guid? workItemId) => RequirementId == requirementId && WorkItemId == workItemId;

        public void Approve(Guid userId, DateTime at, UserRole? role = null)
        {
            EnsurePending();
            Status = SuggestionStatus.Approved;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        public void Reject(RejectionReason? reason, string? comment, Guid userId, DateTime at, UserRole? role = null)
        {
            EnsurePending();
            if (reason is null || !Enum.IsDefined(reason.Value))
            {
                throw new DomainException("Choose a reason");
            }

            if (reason == Mappings.RejectionReason.Other && string.IsNullOrWhiteSpace(comment))
            {
                throw new DomainException("Explain the reason in a comment");
            }

            Status = SuggestionStatus.Rejected;
            RejectionReason = reason;
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim().Length > 1000 ? comment.Trim()[..1000] : comment.Trim();
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // R14: asking again replaces the pending ones, never the decided ones
        public void Replace()
        {
            if (IsPending)
            {
                Status = SuggestionStatus.Replaced;
            }
        }

        // ---------------------------------------------------------- US-145: a rejection holds until what was judged changes

        // R1: the fingerprint of both sides when it was rejected ("item|requirement"); null for rejections before US-145
        public string? RejectedFingerprint { get; private set; }

        // R4: on a suggestion that comes back, the earlier rejection and what changed, e.g. "Rejected before by Bo …"
        public string? EarlierRejection { get; private set; }

        public void JudgedOn(string fingerprint)
        {
            if (Status != SuggestionStatus.Rejected)
            {
                throw new DomainException("Only a rejection keeps what was judged");
            }

            RejectedFingerprint = fingerprint;
        }

        // R2, R7: blocks the pair while neither side changed; a rejection without a fingerprint holds until it gets one
        public bool StillRejects(string currentFingerprint)
            => Status == SuggestionStatus.Rejected && (RejectedFingerprint is null || RejectedFingerprint == currentFingerprint);

        // R6: a person takes the rejection back
        public void UndoRejection()
        {
            if (Status != SuggestionStatus.Rejected)
            {
                throw new DomainException("Only a rejected suggestion can be taken back");
            }

            Status = SuggestionStatus.RejectionUndone;
        }

        public void NoteEarlierRejection(string? note)
            => EarlierRejection = string.IsNullOrWhiteSpace(note) ? null : note.Length <= 300 ? note : note[..300];

        private void EnsurePending()
        {
            if (!IsPending)
            {
                throw new DomainException("The suggestion has already been decided");
            }
        }
    }
}
