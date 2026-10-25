namespace ComplianceTracker.Domain.Users
{
    public class User
    {
        public Guid Id { get; } = Guid.NewGuid();

        // Login-systemets faste id ("sub" fra Keycloak). Brugeren genkendes på det, ikke på navn eller e-mail.
        public string ExternalId { get; private set; }

        public string Name { get; set; }
        public string Email { get; private set; }
        public UserRole Role { get; set; }

        internal User(string externalId, string name, string email, UserRole role)
        {
            ExternalId = externalId;
            Name = name;
            Email = email;
            Role = role;
        }
    }
}
