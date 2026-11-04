using ComplianceTracker.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;

namespace ComplianceTracker.Web.Security
{
    // US-088: how long a sign-in lasts. Settings in the section "Session".
    public class SignInSessionOptions
    {
        public const string Section = "Session";

        // R1: the session ends after this long without a request
        public int IdleMinutes { get; set; } = 30;

        // R2: and after this long at most, also with activity
        public int AbsoluteHours { get; set; } = 8;
    }

    // US-088: the sign-in cookie remembers when the session started and when the user was last seen.
    // Every request checks both against the app's clock; a session that is too old is ended and logged,
    // and the user is sent to sign in again, coming back to the page they asked for (R3).
    public static class SessionLifetime
    {
        private const string StartedKey = ".session_started";
        private const string SeenKey = ".session_seen";

        // How often the cookie is written again to move "last seen" forward, so not every response sets a cookie
        private static readonly TimeSpan SeenPrecision = TimeSpan.FromMinutes(1);

        public static void Configure(CookieAuthenticationOptions cookie, IOptions<SignInSessionOptions> settings, bool development)
        {
            var session = settings.Value;

            // R4. Locally the http profile has no https, so there the cookie is only Secure on https.
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

            // The ticket itself lasts the absolute time; the idle time is checked below
            cookie.ExpireTimeSpan = TimeSpan.FromHours(session.AbsoluteHours);
            cookie.SlidingExpiration = false;

            cookie.Events.OnSigningIn = context =>
            {
                var now = Now(context.HttpContext);
                context.Properties.Items[StartedKey] = Format(now);
                context.Properties.Items[SeenKey] = Format(now);
                return Task.CompletedTask;
            };

            cookie.Events.OnValidatePrincipal = ValidateAsync;
        }

        private static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var session = context.HttpContext.RequestServices.GetRequiredService<IOptions<SignInSessionOptions>>().Value;
            var now = Now(context.HttpContext);
            var started = Read(context.Properties, StartedKey);
            var seen = Read(context.Properties, SeenKey);

            string? reason = null;
            if (started is { } start && now - start > TimeSpan.FromHours(session.AbsoluteHours))
            {
                reason = $"Session ended after {session.AbsoluteHours} hours";
            }
            else if (seen is { } last && now - last > TimeSpan.FromMinutes(session.IdleMinutes))
            {
                reason = $"Timed out after {session.IdleMinutes} minutes without activity";
            }

            if (reason is not null)
            {
                // Read before RejectPrincipal, which forgets the user
                var externalId = context.Principal?.FindFirstValue("sub");

                // The cookie is removed; the request goes on as signed out, so a closed page sends the user to sign in
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // R7
                if (externalId is not null)
                {
                    var activity = context.HttpContext.RequestServices.GetRequiredService<UserActivityService>();
                    await activity.RecordSessionEndedAsync(externalId, reason, context.HttpContext.RequestAborted);
                }

                return;
            }

            // A cookie from before this change gets its times now; "last seen" moves forward at most once a minute
            if (started is null || seen is null || now - seen.Value >= SeenPrecision)
            {
                context.Properties.Items[StartedKey] = Format(started ?? now);
                context.Properties.Items[SeenKey] = Format(now);
                context.ShouldRenew = true;
            }
        }

        // The app's clock (US-016), so the tests can move time
        private static DateTimeOffset Now(HttpContext context)
            => context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

        private static string Format(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);

        private static DateTimeOffset? Read(AuthenticationProperties properties, string key)
            => properties.Items.TryGetValue(key, out var value)
               && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? time
                : null;
    }
}
