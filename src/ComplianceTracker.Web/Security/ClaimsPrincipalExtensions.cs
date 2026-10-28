using ComplianceTracker.Domain.Users;
using System.Security.Claims;

namespace ComplianceTracker.Web.Security
{
    // Den indloggede brugers identitet fra Keycloak (claims i login-cookien)
    public static class ClaimsPrincipalExtensions
    {
        public static string ExternalId(this ClaimsPrincipal user)
            => user.FindFirstValue("sub")
               ?? throw new InvalidOperationException("The signed-in user has no 'sub' claim.");

        public static UserIdentity ToUserIdentity(this ClaimsPrincipal user) => new(
            user.ExternalId(),
            user.FindFirstValue("name") ?? "",
            user.FindFirstValue("email") ?? "");
    }
}