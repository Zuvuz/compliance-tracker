namespace ComplianceTracker.Domain.Users
{
    // Den indloggede persons identitet, som den kommer fra login.
    // ExternalId er login-systemets faste id ("sub"), som aldrig genbruges (ASVS V10.5.2).
    public sealed record UserIdentity(string ExternalId, string Name, string Email);
}
