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

        public Guid DecidedById { get; private set; }

        // US-010: the role of the person when they decided
        public UserRole? DecidedRole { get; private set; }
        public DateTime DecidedAt { get; private set; }

        public Guid? RevokedById { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        // The requirement is not applicable as long as this is not revoked
        public bool IsActive => RevokedAt is null;

        // For EF Core only
        private RequirementApplicability() { }

        public static RequirementApplicability NotApplicable(Guid projectId, Guid requirementId, string reason, Guid userId, DateTime at, UserRole? role = null)
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
                DecidedById = userId,
                DecidedRole = role,
                DecidedAt = at
            };
        }

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
