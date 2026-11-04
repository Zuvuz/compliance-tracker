using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ComplianceTracker.Specs.Support
{
    // US-088: signs a test user in with the app's real sign-in cookie, as if Keycloak had just confirmed them.
    // Added only to the test app (TestApp.WithCookieSignIn) as the first step of the pipeline.
    // GET /test/cookie-sign-in?user=Anna gives the cookie; everything after that is the app's own cookie handling.
    public sealed class CookieSignIn : IStartupFilter
    {
        public const string Path = "/test/cookie-sign-in";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, nextStep) =>
                {
                    if (context.Request.Path != Path)
                    {
                        await nextStep();
                        return;
                    }

                    var identity = TestUsers.Identity(context.Request.Query["user"].ToString());
                    var claims = new List<Claim>
                    {
                        new("sub", identity.ExternalId),
                        new("name", identity.Name),
                        new("email", identity.Email)
                    };
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "name", "role"));
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                });
                next(app);
            };
    }
}
