using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ComplianceTracker.Specs.Support
{
    // Test-login: erstatter Keycloak i de fleste webtests.
    // Testen sætter headeren X-Test-User til et navn, og så er klienten logget ind som den bruger,
    // med de samme claims som Keycloak giver ("sub", "name", "email").
    // Uden headeren er klienten ikke logget ind, og lukkede sider sender til /test-sign-in.
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var name = Request.Headers[UserHeader].ToString();
            if (string.IsNullOrEmpty(name))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = TestUsers.Identity(name);
            var email = Request.Headers[EmailHeader].ToString();

            var claims = new[]
            {
                new Claim("sub", identity.ExternalId),
                new Claim("name", identity.Name),
                new Claim("email", string.IsNullOrEmpty(email) ? identity.Email : email)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }

        // Svarer til, at appen sender brugeren til Keycloaks login-side
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect(SignInPath);
            return Task.CompletedTask;
        }
    }
}
