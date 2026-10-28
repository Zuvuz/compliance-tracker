using ComplianceTracker.Domain.Users;
using System.Security.Claims;

namespace ComplianceTracker.Web.Security
{
    // Reads the signed-in user's identity from Keycloak (the claims stored in the sign-in cookie).
    // Extension methods, so controllers can write User.ExternalId() and User.ToUserIdentity().
    public static class ClaimsPrincipalExtensions
    {
        // Keycloak's permanent id for the user ("sub"). Every signed-in user has one.
        public static string ExternalId(this ClaimsPrincipal user)
            => user.FindFirstValue("sub")
               ?? throw new InvalidOperationException("The signed-in user has no 'sub' claim.");

        // Id, name and e-mail together, e.g. for creating an organisation.
        // Not called "Identity", because ClaimsPrincipal already has a property with that name.
        public static UserIdentity ToUserIdentity(this ClaimsPrincipal user) => new(
            user.ExternalId(),
            user.FindFirstValue("name") ?? "",
            user.FindFirstValue("email") ?? "");
    }
}
