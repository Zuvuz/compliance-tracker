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

        // US-105 R5: the minimum score in force when it was made, so old and new suggestions can be compared
        public double MinimumScore { get; private set; }

        // US-118 R4: with a reranker, Score is its score and this is the search score it had before; null without one
        public double? SearchScore { get; private set; }
        public string? RankingModel { get; private set; }
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
            Guid projectId, Guid evidenceId, Guid requirementId, double score, int rank, string embeddingModel, string promptVersion, DateTime at, double minimumScore = 0)
            => new()
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                RequirementId = requirementId,
                Score = Math.Round(Math.Clamp(score, 0, 1), 4),
                Rank = rank,
                EmbeddingModel = embeddingModel,
                PromptVersion = promptVersion,
                MinimumScore = minimumScore,
                CreatedAt = at
            };

        // US-118 R4: made from a reranked candidate
        public EvidenceLinkSuggestion Reranked(double? searchScore, string? model)
        {
            if (searchScore is not null && !string.IsNullOrWhiteSpace(model))
            {
                SearchScore = Math.Round(Math.Clamp(searchScore.Value, 0, 1), 4);
                RankingModel = model.Length <= 200 ? model : model[..200];
            }

            return this;
        }

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

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }
}
