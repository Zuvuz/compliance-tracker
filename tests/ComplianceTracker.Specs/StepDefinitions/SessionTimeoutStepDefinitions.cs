using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-088: the session's idle and absolute timeouts (Web/Security/SessionLifetime), with the real sign-in cookie.
    // The client keeps cookies like a browser and does not follow redirects, so a sign-in redirect can be seen.
    [Binding]
    public sealed class SessionTimeoutStepDefinitions : IDisposable
    {
        private readonly DatabaseScenario _database;
        private readonly ScenarioClock _clock;
        private TestApp? _app;
        private HttpClient? _browser;
        private HttpResponseMessage? _signIn;
        private HttpResponseMessage? _response;

        public SessionTimeoutStepDefinitions(DatabaseScenario database, ScenarioClock clock)
        {
            _database = database;
            _clock = clock;
        }

        public void Dispose()
        {
            _browser?.Dispose();
            _app?.Dispose();
        }

        [Given("{string} has signed in with the sign-in cookie")]
        public async Task GivenHasSignedInWithTheCookie(string name)
        {
            _app = TestApp.WithCookieSignIn(_database.ConnectionString, _clock);
            _browser = _app.Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });
            _signIn = await _browser.GetAsync($"{CookieSignIn.Path}?user={Uri.EscapeDataString(name)}");
            Assert.Equal(HttpStatusCode.NoContent, _signIn.StatusCode);
        }

        [When("she opens {string}")]
        public async Task WhenSheOpens(string path)
            => _response = await _browser!.GetAsync(path);

        // R2: one request every 20 minutes keeps the session from going idle
        [When("she opens a page every {int} minutes until {string}")]
        public async Task WhenSheOpensAPageEvery(int minutes, string until)
        {
            var end = ClockStepDefinitions.Parse(until);
            for (var time = _clock.GetUtcNow().AddMinutes(minutes); time <= end; time = time.AddMinutes(minutes))
            {
                _clock.SetUtcNow(time);
                _response = await _browser!.GetAsync("/Projects");
                Assert.True(_response.StatusCode == HttpStatusCode.OK, $"At {time:HH:mm} the page gave {_response.StatusCode}, before the session should end.");
            }
        }

        [Then("she sees the page")]
        public void ThenSheSeesThePage()
            => Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);

        // R3: to Keycloak's sign-in, with the page she asked for as the way back
        [Then("she is sent to sign in, coming back to {string}")]
        public void ThenSheIsSentToSignIn(string path)
        {
            Assert.Equal(HttpStatusCode.Found, _response!.StatusCode);
            var location = _response.Headers.Location!.ToString();
            Assert.StartsWith(TestApp.FakeKeycloakSignInPage, location);

            // The way back travels inside the sign-in request's "state", protected by the app; the app's own options read it
            var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(location).Query)["state"].ToString();
            var oidc = _app!.Factory.Services
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>>()
                .Get(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme);
            Assert.Equal(path, new Uri(oidc.StateDataFormat.Unprotect(state)!.RedirectUri!, UriKind.RelativeOrAbsolute) is { IsAbsoluteUri: true } uri
                ? uri.PathAndQuery
                : oidc.StateDataFormat.Unprotect(state)!.RedirectUri);

            // The old cookie is removed
            Assert.Contains(_response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
                c => c.StartsWith(".AspNetCore.Cookies=;", StringComparison.Ordinal));
        }

        [Then("the activity log for {string} has {string} with {string}")]
        public async Task ThenTheActivityLogHasWith(string organisationName, string action, string details)
        {
            await using var db = TestDatabase.CreateContext(_database.ConnectionString);
            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var expected = Enum.Parse<ActivityAction>(action);
            Assert.True(await db.ActivityLog.AnyAsync(e => e.OrganizationId == organisation.Id && e.Action == expected && e.Details == details),
                $"No {action} entry with \"{details}\".");
        }

        // R4
        [Then("the sign-in cookie is {string}, {string} and {string}")]
        public void ThenTheCookieIs(string first, string second, string third)
        {
            var cookie = _signIn!.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal))
                .ToLowerInvariant();
            Assert.Contains(first, cookie);
            Assert.Contains(second, cookie);
            Assert.Contains(third, cookie);
        }
    }
}
