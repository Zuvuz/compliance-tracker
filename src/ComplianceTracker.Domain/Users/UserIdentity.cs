namespace ComplianceTracker.Domain.Users
{
    // The identity of the signed-in person, as it comes from sign-in.
    // ExternalId is the sign-in system's permanent id ("sub"), which is never reused (ASVS V10.5.2).
    // The domain does not know it comes from Keycloak; the web app builds it from the sign-in cookie.
    public sealed record UserIdentity(string ExternalId, string Name, string Email);
}
