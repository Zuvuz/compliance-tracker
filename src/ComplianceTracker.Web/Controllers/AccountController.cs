using ComplianceTracker.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // Sign in and sign out through Keycloak (US-004). The app never sees passwords.
    public class AccountController : Controller
    {
        private readonly UserActivityService _activity;

        public AccountController(UserActivityService activity)
        {
            _activity = activity;
        }

        // Sends the user to Keycloak's sign-in page, and back to the front page afterwards.
        // AllowAnonymous: you must be able to reach this page before you are signed in.
        [AllowAnonymous]
        public IActionResult Login()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = "/" });
        }

        // Signs out of both the app (deletes the cookie) and Keycloak; otherwise the next
        // "Sign in" would let the user straight back in without a password.
        // POST + antiforgery token, so another website cannot sign the user out.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await _activity.RecordSignOutAsync(cancellationToken); // R6: log the sign-out

            // The built-in SignOut: first the cookie, then a redirect to Keycloak's sign-out page
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
