using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ComplianceTracker.Specs.Support
{
    // Test sign-in: replaces Keycloak in most web tests.
    // The test sets the header X-Test-User to a name, and the client is then signed in as that user,
    // with the same claims Keycloak gives ("sub", "name", "email").
    // Without the header the client is not signed in, and closed pages redirect to /test-sign-in.
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string UserHeader = "X-Test-User";
        public const string EmailHeader = "X-Test-Email";

        // US-132: how long ago the person signed in, in minutes
        public const string SignedInMinutesAgoHeader = "X-Test-Signed-In-Minutes-Ago";

        // US-010: Keycloak realm roles, comma separated, e.g. "system-admin"
        public const string RolesHeader = "X-Test-Roles";
        public const string SignInPath = "/test-sign-in";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        // Runs on every request: who is the user?
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var name = Request.Headers[UserHeader].ToString();
            if (string.IsNullOrEmpty(name))
            {
                return Task.FromResult(AuthenticateResult.NoResult()); // not signed in
            }

            // Same fixed id for the same name, so a test user is recognised across requests
            var identity = TestUsers.Identity(name);
            var email = Request.Headers[EmailHeader].ToString();

            var claims = new List<Claim>
            {
                new Claim("sub", identity.ExternalId),
                new Claim("name", identity.Name),
                new Claim("email", string.IsNullOrEmpty(email) ? identity.Email : email)
            };

            // Like Keycloak's realm role mapper: one "roles" claim per realm role
            // US-132 R2: when the person signed in; "now" unless the scenario says it was earlier
            var minutesAgo = int.TryParse(Request.Headers[SignedInMinutesAgoHeader].ToString(), out var minutes) ? minutes : 0;
            claims.Add(new Claim(ComplianceTracker.Web.Security.HttpContextCurrentUser.SignedInClaim,
                (Context.RequestServices.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System).GetUtcNow().AddMinutes(-minutesAgo).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));

            foreach (var role in Request.Headers[RolesHeader].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("roles", role));
            }

            // "name" is the claim User.Identity.Name reads, like in Program.cs
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }

        // Runs when a closed page is opened without sign-in.
        // Matches the app sending the user to Keycloak's sign-in page.
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect(SignInPath);
            return Task.CompletedTask;
        }
    }
}
