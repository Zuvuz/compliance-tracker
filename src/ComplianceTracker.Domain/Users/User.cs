namespace ComplianceTracker.Domain.Users
{
    // A person who is a member of an organisation and has a role in it
    public class User
    {
        public Guid Id { get; } = Guid.NewGuid();

        // The sign-in system's permanent id ("sub" from Keycloak). Users are recognised by this,
        // never by name or e-mail, which can change or be shared (ASVS V10.5.2).
        public string ExternalId { get; private set; }

        public string Name { get; set; }
        public string Email { get; private set; }
        // Changed only through Organization.ChangeRole, so the "at least one Admin" rule always runs (US-010 R3)
        public UserRole Role { get; internal set; }

        // US-090: a removed member is kept, so their name stays on what they did; they can no longer act
        public DateTime? RemovedAt { get; private set; }
        public Guid? RemovedById { get; private set; }
        public bool IsActive => RemovedAt is null;

        // A removed member may do nothing
        // US-096 R4: the member has hidden the "Get started" card
        public bool HidesGetStarted { get; private set; }

        public void SetGetStartedHidden(bool hidden) => HidesGetStarted = hidden;

        // US-132: the member signs reports for the management (a permission given to the person, not a role)
        public bool SignsForManagement { get; internal set; }

        public bool Can(Permission permission) => IsActive && RolePermissions.Can(Role, permission);

        internal void Remove(Guid removedById, DateTime at)
        {
            RemovedAt = at;
            RemovedById = removedById;
        }

        // R7: invited again, the person comes back with the invitation's role
        internal void Restore(UserRole role)
        {
            RemovedAt = null;
            RemovedById = null;
            Role = role;
        }

        // US-095 R3: a removed member whose personal data is erased: the name and e-mail are replaced by a pseudonym, and
        // the link to Keycloak is cut. What they did stays, under the pseudonym.
        public const string ErasedPrefix = "erased:";

        public bool IsErased => ExternalId.StartsWith(ErasedPrefix, StringComparison.Ordinal);

        // e.g. "Former member 7F3A"
        public string Pseudonym => $"Former member {Id.ToString("N")[..4].ToUpperInvariant()}";

        internal void Pseudonymise()
        {
            Name = Pseudonym;
            Email = $"{Id:N}@erased.invalid";
            ExternalId = ErasedPrefix + Id.ToString("N");
            HidesGetStarted = false;
            SignsForManagement = false;
        }

        // Internal: users are only created through Organization, so they always belong to one.
        // EF Core also uses this constructor; the parameter names match the property names.
        internal User(string externalId, string name, string email, UserRole role)
        {
            ExternalId = externalId;
            Name = name;
            Email = email;
            Role = role;
        }
    }
}
