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

        public bool Can(Permission permission) => RolePermissions.Can(Role, permission);

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
