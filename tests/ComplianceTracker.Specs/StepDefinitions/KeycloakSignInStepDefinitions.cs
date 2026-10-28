using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // The real sign-in: the web app talks OpenID Connect with a Keycloak container (US-004 R2, ASVS V10.2.1).
    // The test plays the browser and moves the user back and forth between the web app and Keycloak,
    // exactly as a browser follows redirects.
    [Binding]
    public class KeycloakSignInStepDefinitions
    {
        private readonly DatabaseScenario _database;
        private TestApp? _app;
        private HttpClient? _appClient;
        private HttpResponseMessage? _response;

        public KeycloakSignInStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        [AfterScenario("keycloak")]
        public void StopApplication()
        {
            _appClient?.Dispose();
            _app?.Dispose();
        }

        [Given("Keycloak has a user {string} with e-mail {string}")]
        public async Task GivenKeycloakHasAUser(string username, string email)
        {
            await TestKeycloak.StartAsync();
            await TestKeycloak.CreateUserAsync(username, email);
        }

        [When("I sign in through Keycloak as {string}")]
        public async Task WhenISignInThroughKeycloakAs(string username)
        {
            _app = TestApp.WithKeycloak(_database.ConnectionString, TestKeycloak.Authority, TestKeycloak.ClientSecret);
            // https, like a real browser: the sign-in cookies are "Secure" and are only sent over https
            _appClient = _app.Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

            // The "browser" towards Keycloak: does not follow redirects by itself
            using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
            var keycloakCookies = new Dictionary<string, string>();

            // 1. The web app sends the user to Keycloak (with PKCE)
            var toKeycloak = await _appClient.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.Redirect, toKeycloak.StatusCode);

            // 2. Keycloak's sign-in page
            var loginPage = await SendAsync(browser, keycloakCookies, new HttpRequestMessage(HttpMethod.Get, toKeycloak.Headers.Location));
            loginPage.EnsureSuccessStatusCode();
            var form = (await WebScenario.ParseAsync(loginPage)).QuerySelector<IHtmlFormElement>("#kc-form-login");
            Assert.NotNull(form);

            // 3. The user signs in at Keycloak. The web app never sees the password.
            var afterLogin = await SendAsync(browser, keycloakCookies, new HttpRequestMessage(HttpMethod.Post, form.GetAttribute("action"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["password"] = TestKeycloak.PasswordFor(username),
                    ["credentialId"] = ""
                })
            });
            Assert.True(afterLogin.StatusCode == HttpStatusCode.Found,
                $"Keycloak did not send the user back. Status {afterLogin.StatusCode}: {await afterLogin.Content.ReadAsStringAsync()}");

            // 4. Back to the web app with a code, which the web app exchanges for tokens at Keycloak
            var callback = await _appClient.GetAsync(afterLogin.Headers.Location!.PathAndQuery);
            Assert.True(callback.StatusCode == HttpStatusCode.Redirect,
                $"The app did not accept the sign-in. Status {callback.StatusCode}: {await callback.Content.ReadAsStringAsync()}");

            // 5. Now the user is signed in (cookie) and can open a closed page
            _response = await _appClient.GetAsync("/Organizations/Create");
        }

        // Keycloak sets its cookies as "Secure". Chrome still sends them to http://localhost,
        // but .NET's CookieContainer does not. So the test remembers Keycloak's cookies itself.
        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient browser, Dictionary<string, string> cookies, HttpRequestMessage request)
        {
            if (cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
            }

            var response = await browser.SendAsync(request);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    var nameValue = setCookie.Split(';', 2)[0].Split('=', 2);
                    cookies[nameValue[0]] = nameValue.Length > 1 ? nameValue[1] : "";
                }
            }

            return response;
        }

        [Then("I am signed in to the application as {string}")]
        public async Task ThenIAmSignedInToTheApplicationAs(string username)
        {
            Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);

            var page = await WebScenario.ParseAsync(_response);
            var signedInAs = page.QuerySelector("#signed-in-user")?.TextContent.Trim() ?? "";
            Assert.Contains(username, signedInAs, StringComparison.OrdinalIgnoreCase);
        }
    }
}
