using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Mappings
{
    // A suggestion from the local AI that a user story covers a requirement (US-020).
    // Every suggestion is kept, also those nobody decides on, with the models and prompt version behind it
    // (US-036 R3, AI Act Art. 12). A person approves or rejects it once; the decision never changes (US-036 R4, B13).
    public class MappingSuggestion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid WorkItemId { get; private set; }
        public Guid RequirementId { get; private set; }

        // How similar the story and the requirement are, from 0 to 1 (the embedding model's cosine similarity)
        public double Score { get; private set; }

        // 1 = the best suggestion for the story in its batch (US-036 R1)
        public int Rank { get; private set; }

        // Written afterwards by the chat model; null until then
        public string? Rationale { get; private set; }

        // Which models and prompt made it, e.g. "nomic-embed-text", "qwen2.5:3b", "mapping-v1"
        public string EmbeddingModel { get; private set; } = "";
        public string? RationaleModel { get; private set; }
        public string PromptVersion { get; private set; } = "";

        // US-060: what the AI was told about the project for the rationale, e.g. "purpose, scope, risk 'Stolen sessions'"
        public string? ContextUsed { get; private set; }

        // US-105 R5: the minimum score in force when it was made, so old and new suggestions can be compared
        public double MinimumScore { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }

        // US-010: the role of the person when they decided
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public RejectionReason? RejectionReason { get; private set; }
        public string? Comment { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private MappingSuggestion() { }

        public static MappingSuggestion Create(
            Guid projectId, Guid workItemId, Guid requirementId, double score, int rank,
            string embeddingModel, string promptVersion, DateTime at, double minimumScore = 0)
            => new()
            {
                ProjectId = projectId,
                WorkItemId = workItemId,
                RequirementId = requirementId,
                Score = Math.Round(Math.Clamp(score, 0, 1), 4),
                Rank = rank,
                EmbeddingModel = embeddingModel,
                PromptVersion = promptVersion,
                MinimumScore = minimumScore,
                CreatedAt = at
            };

        // The chat model's short explanation; written once
        public void SetRationale(string rationale, string model, string? contextUsed = null)
        {
            if (Rationale is not null)
            {
                return;
            }

            Rationale = Cut(rationale.Trim(), 1000);
            RationaleModel = model;
            ContextUsed = string.IsNullOrWhiteSpace(contextUsed) ? null : Cut(contextUsed, 1000);
        }

        // US-020 R6: the caller creates the link
        public void Approve(Guid userId, DateTime at, UserRole? role = null)
        {
            EnsurePending();
            DecidedRole = role;
            Status = SuggestionStatus.Approved;
            DecidedById = userId;
            DecidedAt = at;
        }

        // US-020 R7: a reason from the fixed list is required; "Other" also needs a comment
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
            DecidedRole = role;
            RejectionReason = reason;
            Comment = string.IsNullOrWhiteSpace(comment) ? null : Cut(comment.Trim(), 1000);
            DecidedById = userId;
            DecidedAt = at;
        }

        // US-020 R8: a new run replaces the pending suggestions; they are kept, marked as replaced
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

    // Stored as text
    public enum SuggestionStatus
    {
        Pending,
        Approved,
        Rejected,

        // A newer run replaced it before anybody decided
        Replaced
    }

    // US-020 R7, B14: the fixed list of reasons
    public enum RejectionReason
    {
        NotRelevant,
        WrongRequirement,
        TooBroad,
        AlreadyMapped,
        InsufficientInformation,
        Other
    }
}
