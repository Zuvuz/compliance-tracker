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

        // US-196 R1: the invitee's e-mail; null for a link made without one (before US-196). R4: only that person may use it.
        public string? Email { get; private set; }

        // US-196 R2: for a consultant or an auditor, the last day of access; for an auditor, optionally one project
        public DateOnly? AccessUntil { get; private set; }
        public Guid? OnlyProjectId { get; private set; }

        public const int MaxEmailLength = 254;

        public Guid? AcceptedById { get; private set; }
        public DateTime? AcceptedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        public bool IsUsable(DateTime now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

        // For EF Core only
        private Invitation() { }

        public static Invitation Create(Guid organizationId, UserRole role, string tokenHash, Guid createdById, DateTime at)
            => Create(organizationId, role, tokenHash, createdById, at, email: null, accessUntil: null, onlyProjectId: null);

        // US-196 R1, R2: the invitee's e-mail, and for an external role an end date (from today) and, for an auditor, one project
        public static Invitation Create(Guid organizationId, UserRole role, string tokenHash, Guid createdById, DateTime at,
            string? email, DateOnly? accessUntil, Guid? onlyProjectId)
        {
            if (!Enum.IsDefined(role))
            {
                throw new DomainException("Choose a role");
            }

            if (accessUntil is { } until)
            {
                if (!RolePermissions.IsExternal(role))
                {
                    throw new DomainException("An end date is only for a consultant or an auditor");
                }

                if (until < DateOnly.FromDateTime(at))
                {
                    throw new DomainException("The end date must be today or later");
                }
            }

            if (onlyProjectId is not null && role != UserRole.Auditor)
            {
                throw new DomainException("Only an auditor is limited to one project");
            }

            return new Invitation
            {
                OrganizationId = organizationId,
                Role = role,
                TokenHash = tokenHash,
                CreatedById = createdById,
                CreatedAt = at,
                ExpiresAt = at.AddDays(ValidDays),
                Email = NormaliseEmail(email),
                AccessUntil = accessUntil,
                OnlyProjectId = onlyProjectId
            };
        }

        // R1: "  Carl@Advisor.example " -> "carl@advisor.example"; null when empty. A plain check: one @, no spaces.
        public static string? NormaliseEmail(string? email)
        {
            var value = (email ?? "").Trim().ToLowerInvariant();
            if (value.Length == 0)
            {
                return null;
            }

            var at = value.IndexOf('@');
            if (value.Length > MaxEmailLength || at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1
                || value.Any(char.IsWhiteSpace) || !value[(at + 1)..].Contains('.'))
            {
                throw new DomainException("Give a valid e-mail address");
            }

            return value;
        }

        // R4: an invitation with an e-mail can only be used by the person signed in with that e-mail
        public void EnsureFor(string? signedInEmail)
        {
            if (Email is { } email && !string.Equals(email, (signedInEmail ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException($"This invitation is for {email}");
            }
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
