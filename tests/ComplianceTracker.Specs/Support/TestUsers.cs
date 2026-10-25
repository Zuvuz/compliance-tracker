using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Specs.Support
{
    // Laver en fast testidentitet ud fra et navn, fx "Anna" → anna-id, anna@example.com
    public static class TestUsers
    {
        public static UserIdentity Identity(string name) => new(
            $"{name.ToLowerInvariant()}-id",
            name,
            $"{name.ToLowerInvariant()}@example.com");
    }
}
