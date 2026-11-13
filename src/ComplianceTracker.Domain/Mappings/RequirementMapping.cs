using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-037 R2: how much two requirements in different frameworks ask for the same thing
    public enum MappingStrength
    {
        Same,
        Partly,
        Related
    }

    // US-037: the AI suggests that a requirement in one framework version asks for the same as one in another; a person
    // approves or rejects it (R3). A match belongs to the organisation and to exactly those two versions (R4). An approved
    // "Same" lets coverage pass from one to the other (R5).
    public class RequirementMapping
    {
        public const double SameFrom = 0.85;
        public const double PartlyFrom = 0.7;
        // R2: texts less alike than this are not suggested at all, whatever the story limit is
        public const double RelatedFrom = 0.5;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid FromVersionId { get; private set; }
        public Guid ToVersionId { get; private set; }
        public Guid FromRequirementId { get; private set; }
        public Guid ToRequirementId { get; private set; }
        public MappingStrength Strength { get; private set; }
        public double Score { get; private set; }
        public int Rank { get; private set; }
        public string Rationale { get; private set; } = "";
        public string Model { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public RejectionReason? RejectionReason { get; private set; }
        public string? Comment { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;
        public bool IsApprovedSame => Status == SuggestionStatus.Approved && Strength == MappingStrength.Same;

        // For EF Core only
        private RequirementMapping() { }

        public static RequirementMapping Suggest(Guid organizationId, Guid fromVersionId, Guid toVersionId, Guid fromRequirementId, Guid toRequirementId,
            double score, int rank, string rationale, string model, DateTime at)
        {
            if (fromVersionId == toVersionId)
            {
                throw new DomainException("Choose two different frameworks");
            }

            if (rank < 1)
            {
                throw new DomainException("The rank starts at 1");
            }

            var clamped = double.IsNaN(score) ? 0 : Math.Round(Math.Clamp(score, 0, 1), 4);
            return new RequirementMapping
            {
                OrganizationId = organizationId,
                FromVersionId = fromVersionId,
                ToVersionId = toVersionId,
                FromRequirementId = fromRequirementId,
                ToRequirementId = toRequirementId,
                Score = clamped,
                Strength = StrengthOf(clamped),
                Rank = rank,
                Rationale = rationale.Length <= 500 ? rationale : rationale[..500],
                Model = model,
                CreatedAt = at
            };
        }

        // R2: the strength follows how alike the two texts are
        public static MappingStrength StrengthOf(double score)
            => score >= SameFrom ? MappingStrength.Same : score >= PartlyFrom ? MappingStrength.Partly : MappingStrength.Related;

        public bool Joins(Guid a, Guid b) => (FromRequirementId == a && ToRequirementId == b) || (FromRequirementId == b && ToRequirementId == a);

        public void Approve(Guid userId, UserRole? role, DateTime at)
        {
            EnsurePending();
            Status = SuggestionStatus.Approved;
            Decided(userId, role, at);
        }

        // R3: a reason from the fixed list (B14); "Other" also needs a comment
        public void Reject(RejectionReason? reason, string? comment, Guid userId, UserRole? role, DateTime at)
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
            Decided(userId, role, at);
        }

        // Asking again replaces the pending ones, never the decided ones
        public void Replace()
        {
            if (IsPending)
            {
                Status = SuggestionStatus.Replaced;
            }
        }

        private void Decided(Guid userId, UserRole? role, DateTime at)
        {
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
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
