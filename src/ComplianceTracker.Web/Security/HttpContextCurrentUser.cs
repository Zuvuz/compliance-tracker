using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Web.Security
{
    // ICurrentUser for the web app (US-029 R1): the person in the sign-in cookie of the current request.
    // Registered as scoped, so it always belongs to exactly one request.
    public class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public UserIdentity Identity
            => (_httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("There is no current request.")).ToUserIdentity();
    }
}
