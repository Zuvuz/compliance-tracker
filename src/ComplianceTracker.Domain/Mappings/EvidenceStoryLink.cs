using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-146 R5: evidence shows that a user story or product requirement is done, e.g. a test report for "[US-004] Sign in".
    // Made by a person, or by approving an AI suggestion. Removing keeps it as history.
    public class EvidenceStoryLink
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public Guid WorkItemId { get; private set; }
        public LinkSource Source { get; private set; }
        public Guid LinkedById { get; private set; }
        public UserRole? LinkedRole { get; private set; }
        public DateTime LinkedAt { get; private set; }
        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private EvidenceStoryLink() { }

        public static EvidenceStoryLink Create(
            Guid projectId, Guid evidenceId, Guid workItemId, Guid linkedById, DateTime at, LinkSource source = LinkSource.Manual, UserRole? role = null)
            => new()
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                WorkItemId = workItemId,
                Source = source,
                LinkedById = linkedById,
                LinkedRole = role,
                LinkedAt = at
            };

        public void Remove(Guid userId, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The link is already removed");
            }

            RemovedById = userId;
            RemovedAt = at;
        }
    }

    // US-146 R6: the AI suggests that a piece of evidence shows a user story is done, like EvidenceLinkSuggestion does for
    // requirements (US-059). A person approves (the caller then links them) or rejects it with a reason, once.
    public class EvidenceStorySuggestion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public Guid WorkItemId { get; private set; }
        public double Score { get; private set; }
        public int Rank { get; private set; }
        public string EmbeddingModel { get; private set; } = "";
        public double MinimumScore { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public RejectionReason? RejectionReason { get; private set; }
        public string? Comment { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private EvidenceStorySuggestion() { }

        public static EvidenceStorySuggestion Create(
            Guid projectId, Guid evidenceId, Guid workItemId, double score, int rank, string embeddingModel, DateTime at, double minimumScore = 0)
        {
            if (rank < 1)
            {
                throw new DomainException("The rank starts at 1");
            }

            return new EvidenceStorySuggestion
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                WorkItemId = workItemId,
                Score = double.IsNaN(score) ? 0 : Math.Round(Math.Clamp(score, 0, 1), 4),
                Rank = rank,
                EmbeddingModel = embeddingModel,
                MinimumScore = minimumScore,
                CreatedAt = at
            };
        }

        public void Approve(Guid userId, DateTime at, UserRole? role = null)
        {
            EnsurePending();
            Status = SuggestionStatus.Approved;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // A reason from the fixed list (US-020 R7); "Other" also needs a comment
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
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim() is var c && c.Length > 1000 ? c[..1000] : comment.Trim();
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // The story was linked by hand, or the evidence removed: no decision is needed any more
        public void Replace()
        {
            if (IsPending)
            {
                Status = SuggestionStatus.Replaced;
            }
        }

        private void EnsurePending()
        {
            if (!IsPending)
            {
                throw new DomainException("The suggestion has already been decided");
            }
        }
    }
}
