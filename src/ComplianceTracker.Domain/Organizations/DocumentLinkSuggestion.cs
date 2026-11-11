using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Organizations
{
    // US-136 R2: a suggestion from the local AI that one of the organisation's documents (a policy, a procedure, a risk
    // table) proves a requirement, like EvidenceLinkSuggestion on a project (US-059). It belongs to the document, not to
    // each project's copy, so it is decided once: approving links the document to the requirement by framework and code,
    // and the document then becomes evidence wherever that requirement is followed. Kept with the model and prompt behind
    // it; decided once; a rejected pair is never suggested again.
    public class DocumentLinkSuggestion
    {
        public const int MaxCodeLength = 50;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid DocumentId { get; private set; }

        // The requirement by framework and code, as a document link (OrganisationDocumentLink)
        public Guid FrameworkId { get; private set; }
        public string RequirementCode { get; private set; } = "";

        // Cosine similarity from 0 to 1, and the order it was suggested in (1 is the best of the first batch)
        public double Score { get; private set; }
        public int Rank { get; private set; }

        public string EmbeddingModel { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
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
        private DocumentLinkSuggestion() { }

        public static DocumentLinkSuggestion Create(
            Guid organizationId, Guid documentId, Guid frameworkId, string requirementCode, double score, int rank,
            string embeddingModel, string promptVersion, DateTime at, double minimumScore = 0)
        {
            var code = (requirementCode ?? "").Trim();
            if (code.Length == 0 || code.Length > MaxCodeLength)
            {
                throw new DomainException("A suggestion needs the requirement's code");
            }

            if (rank < 1)
            {
                throw new DomainException("The rank starts at 1");
            }

            return new DocumentLinkSuggestion
            {
                OrganizationId = organizationId,
                DocumentId = documentId,
                FrameworkId = frameworkId,
                RequirementCode = code,
                Score = Math.Round(Math.Clamp(double.IsNaN(score) ? 0 : score, 0, 1), 4),
                Rank = rank,
                EmbeddingModel = embeddingModel,
                PromptVersion = promptVersion,
                MinimumScore = minimumScore,
                CreatedAt = at
            };
        }

        // Whether it is about this framework and code ("A.5.15" and "a.5.15" are the same, as for document links)
        public bool IsFor(Guid frameworkId, string code)
            => FrameworkId == frameworkId && string.Equals(RequirementCode, code.Trim(), StringComparison.OrdinalIgnoreCase);

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
            Comment = string.IsNullOrWhiteSpace(comment) ? null : Cut(comment.Trim(), 1000);
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // The document was removed, or linked to the requirement by hand: the pending suggestion needs no decision
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
