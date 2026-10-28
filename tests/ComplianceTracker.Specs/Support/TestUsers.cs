using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Specs.Support
{
    // Makes a fixed test identity from a name, e.g. "Anna" → anna-id, anna@example.com.
    // Used both when tests create data directly and by TestAuthHandler, so the same name is the same user.
    public static class TestUsers
    {
        public static UserIdentity Identity(string name) => new(
            $"{name.ToLowerInvariant()}-id",
            name,
            $"{name.ToLowerInvariant()}@example.com");
    }
}
