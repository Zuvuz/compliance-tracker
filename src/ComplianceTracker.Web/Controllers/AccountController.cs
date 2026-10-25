using ComplianceTracker.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ComplianceTracker.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserActivityService _activity;

        public AccountController(UserActivityService activity)
        {
            _activity = activity;
        }

        // Sender brugeren til Keycloak og derefter tilbage til forsiden
        [AllowAnonymous]
        public IActionResult Login()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = "/" });
        }

        // Logger ud både i appen (cookien) og i Keycloak.
        // POST + antiforgery, så et andet website ikke kan logge brugeren ud.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var externalId = User.FindFirstValue("sub");
            if (externalId is not null)
            {
                await _activity.RecordSignOutAsync(externalId, cancellationToken); // R6
            }

            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
