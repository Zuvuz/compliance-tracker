using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Organizations
{
    // A link that lets one person join the organisation with a given role (US-009).
    // Only the SHA-256 of the link's token is stored, so the link cannot be rebuilt from the database (R6).
    // Single use, expires after 7 days (R3), and can be revoked (R4). Never deleted, so the history is kept.
    public class Invitation
    {
        public const int ValidDays = 7;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // The role the person gets when they accept
        public UserRole Role { get; private set; }

        // SHA-256 of the token, as hex
        public string TokenHash { get; private set; } = "";

        public Guid CreatedById { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        public Guid? AcceptedById { get; private set; }
        public DateTime? AcceptedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        public bool IsUsable(DateTime now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

        // For EF Core only
        private Invitation() { }

        public static Invitation Create(Guid organizationId, UserRole role, string tokenHash, Guid createdById, DateTime at)
        {
            if (!Enum.IsDefined(role))
            {
                throw new DomainException("Choose a role");
            }

            return new Invitation
            {
                OrganizationId = organizationId,
                Role = role,
                TokenHash = tokenHash,
                CreatedById = createdById,
                CreatedAt = at,
                ExpiresAt = at.AddDays(ValidDays)
            };
        }

        // R2, R3, R4: why a link cannot be used, in words the person understands
        public void EnsureUsable(DateTime now)
        {
            if (RevokedAt is not null)
            {
                throw new DomainException("The invitation has been revoked");
            }

            if (AcceptedAt is not null)
            {
                throw new DomainException("The invitation has already been used");
            }

            if (now >= ExpiresAt)
            {
                throw new DomainException("The invitation has expired");
            }
        }

        public void MarkAccepted(Guid userId, DateTime at)
        {
            EnsureUsable(at);
            AcceptedById = userId;
            AcceptedAt = at;
        }

        public void Revoke(DateTime at)
        {
            EnsureUsable(at);
            RevokedAt = at;
        }
    }
}
