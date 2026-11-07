using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Users;
using System.Security.Claims;

namespace ComplianceTracker.Web.Security
{
    // ICurrentUser for the web app (US-029 R1): the person in the sign-in cookie of the current request.
    // Registered as scoped, so it always belongs to exactly one request.
    public class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CurrentPrincipal _principal;

        public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor, CurrentPrincipal principal)
        {
            _httpContextAccessor = httpContextAccessor;
            _principal = principal;
        }

        public UserIdentity Identity => Principal.ToUserIdentity();

        // US-010: the Keycloak realm role "system-admin", sent in the "roles" claim (realm role mapper)
        public bool IsSystemAdmin => Principal.HasClaim(SystemAdmin.ClaimType, SystemAdmin.Role);

        // US-132 R2: stamped when Keycloak confirmed the person (Program.cs, OnTokenValidated)
        public DateTime? SignedInAt
            => long.TryParse(Principal.FindFirstValue(SignedInClaim), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;

        public const string SignedInClaim = "ct_signed_in_at";

        private ClaimsPrincipal Principal
            => _principal.User
               ?? _httpContextAccessor.HttpContext?.User
               ?? throw new InvalidOperationException("There is no current request.");
    }

    // US-010: how the tracker's owner is recognised. Given in Keycloak, never by the app.
    public static class SystemAdmin
    {
        public const string ClaimType = "roles";
        public const string Role = "system-admin";
        public const string Policy = "SystemAdmin";
    }

    // The signed-in user when it does not come from an HTTP request, e.g. a SignalR hub call (US-018).
    // Scoped, so it only applies to the one call that set it.
    public class CurrentPrincipal
    {
        public ClaimsPrincipal? User { get; set; }
    }
}
