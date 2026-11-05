using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-059 R3, R4: a suggestion from the local AI that an evidence proves a requirement, like MappingSuggestion
    // (US-020, US-036). Every suggestion is kept with the models and prompt behind it. A person approves or rejects it
    // once; the caller then creates the EvidenceLink (B13).
    public class EvidenceLinkSuggestion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public Guid RequirementId { get; private set; }

        // The embedding model's cosine similarity, from 0 to 1
        public double Score { get; private set; }

        // 1 = the best suggestion for the evidence
        public int Rank { get; private set; }

        // Written afterwards by the chat model; marked "AI-generated" on the page
        public string? Rationale { get; private set; }

        public string EmbeddingModel { get; private set; } = "";
        public string? RationaleModel { get; private set; }
        public string PromptVersion { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public RejectionReason? RejectionReason { get; private set; }
        public string? Comment { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private EvidenceLinkSuggestion() { }

        public static EvidenceLinkSuggestion Create(
            Guid projectId, Guid evidenceId, Guid requirementId, double score, int rank, string embeddingModel, string promptVersion, DateTime at)
            => new()
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                RequirementId = requirementId,
                Score = Math.Round(Math.Clamp(score, 0, 1), 4),
                Rank = rank,
                EmbeddingModel = embeddingModel,
                PromptVersion = promptVersion,
                CreatedAt = at
            };

        public void SetRationale(string rationale, string model)
        {
            if (Rationale is not null)
            {
                return;
            }

            Rationale = Cut(rationale.Trim(), 1000);
            RationaleModel = model;
        }

        public void Approve(Guid userId, DateTime at, UserRole? role = null)
        {
            EnsurePending();
            Status = SuggestionStatus.Approved;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // R4: a reason from the fixed list (US-020 R7); "Other" also needs a comment
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
            Comment = string.IsNullOrWhiteSpace(comment) ? null : Cut(comment.Trim(), 1000);
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // The evidence was removed, or the pair was linked by hand: the pending suggestion no longer needs a decision
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

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }
}
