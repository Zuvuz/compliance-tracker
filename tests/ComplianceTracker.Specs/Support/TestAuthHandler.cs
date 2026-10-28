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

            var claims = new[]
            {
                new Claim("sub", identity.ExternalId),
                new Claim("name", identity.Name),
                new Claim("email", string.IsNullOrEmpty(email) ? identity.Email : email)
            };

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
