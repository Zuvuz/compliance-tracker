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

        public UserIdentity Identity
            => (_principal.User
                ?? _httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("There is no current request.")).ToUserIdentity();
    }

    // The signed-in user when it does not come from an HTTP request, e.g. a SignalR hub call (US-018).
    // Scoped, so it only applies to the one call that set it.
    public class CurrentPrincipal
    {
        public ClaimsPrincipal? User { get; set; }
    }
}
