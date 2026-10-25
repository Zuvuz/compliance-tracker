using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    public class AccountController : Controller
    {
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
        public IActionResult Logout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}