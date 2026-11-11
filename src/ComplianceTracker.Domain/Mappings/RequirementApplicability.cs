using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Mappings
{
    // A requirement that does not apply to one project, with the reason (US-043 R5).
    // The CRA's essential requirements apply "where applicable" on the basis of the risk assessment (Art. 13(2), (3)).
    // Making the requirement applicable again sets RevokedAt; the row is never deleted, so the history is kept.
    public class RequirementApplicability
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RequirementId { get; private set; }

        // Why the requirement does not apply, e.g. "No network-facing interface (risk assessment v2)"
        public string Reason { get; private set; } = "";

        // US-144 R14: a risk of the project that justifies the decision, besides the reason (CRA Art. 13(3)); optional
        public Guid? RiskId { get; private set; }

        public Guid DecidedById { get; private set; }

        // US-010: the role of the person when they decided
        public UserRole? DecidedRole { get; private set; }
        public DateTime DecidedAt { get; private set; }

        public Guid? RevokedById { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        // The requirement is not applicable as long as this is not revoked
        public bool IsActive => RevokedAt is null;

        // US-110: carried over to a requirement whose title or text changed in the new version. It still shows, but
        // does not count until a person has checked it against the new text. CarriedFromRequirementId is the
        // requirement it was carried from, so the old text can be shown next to the new.
        public bool NeedsReview { get; private set; }
        public Guid? CarriedFromRequirementId { get; private set; }
        public Guid? ReviewedById { get; private set; }
        public DateTime? ReviewedAt { get; private set; }

        // US-110 R5: "Still correct"
        public void MarkReviewed(Guid userId, DateTime at)
        {
            if (!NeedsReview)
            {
                throw new DomainException("Nothing to review");
            }

            NeedsReview = false;
            ReviewedById = userId;
            ReviewedAt = at;
        }

        // For EF Core only
        private RequirementApplicability() { }

        public static RequirementApplicability NotApplicable(
            Guid projectId, Guid requirementId, string reason, Guid userId, DateTime at, UserRole? role = null, Guid? riskId = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason why the requirement does not apply");
            }

            return new RequirementApplicability
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                Reason = reason.Trim(),
                RiskId = riskId,
                DecidedById = userId,
                DecidedRole = role,
                DecidedAt = at
            };
        }

        // US-069 R6: the same decision on the requirement with the same code in the version the project moves to
        // US-110 R2: textChanged marks it for review
        public RequirementApplicability CarryTo(Guid requirementId, bool textChanged = false)
            => new()
            {
                ProjectId = ProjectId,
                RequirementId = requirementId,
                Reason = Reason,
                RiskId = RiskId,
                DecidedById = DecidedById,
                DecidedRole = DecidedRole,
                DecidedAt = DecidedAt,
                NeedsReview = NeedsReview || textChanged,
                CarriedFromRequirementId = NeedsReview ? CarriedFromRequirementId : RequirementId
            };

        // The requirement applies again
        public void Revoke(Guid userId, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The requirement already applies");
            }

            RevokedById = userId;
            RevokedAt = at;
        }
    }
}
