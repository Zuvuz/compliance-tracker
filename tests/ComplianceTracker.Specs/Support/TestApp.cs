using ComplianceTracker.Infrastructure.WorkItems;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ComplianceTracker.Specs.Support
{
    // Starts the real web app in memory (WebApplicationFactory) against the test database.
    // Requests go straight to the app without a network, but through the full pipeline.
    public sealed class TestApp : IDisposable
    {
        private readonly WebApplicationFactory<Program> _base;

        public WebApplicationFactory<Program> Factory { get; }

        private TestApp(WebApplicationFactory<Program> baseFactory, WebApplicationFactory<Program> factory)
        {
            _base = baseFactory;
            Factory = factory;
        }

        // The web app with test sign-in instead of Keycloak (see TestAuthHandler)
        // The clock replaces TimeProvider.System, so "Given the time is ..." also applies inside the app (US-016)
        // overrides: settings to change, e.g. an empty ClientId to test the start-up check (US-029 R3)
        // github: the pretend GitHub the app reads user stories from (US-018); without one, an empty one
        public static TestApp WithTestSignIn(string connectionString, TimeProvider clock, IDictionary<string, string?>? overrides = null, FakeGitHub? github = null)
        {
            github ??= new FakeGitHub();
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                // Only used when the app redirects to Keycloak on sign-out. There is no real Keycloak in these tests.
                ["Authentication:Keycloak:Authority"] = FakeKeycloak.Issuer,
                ["Authentication:Keycloak:ClientId"] = "compliance-tracker-web",
                ["Authentication:Keycloak:ClientSecret"] = "not-used-in-tests",
                // The tests run the scheduled GitHub check themselves ("the scheduled check runs"), never on a timer
                ["WorkItemSync:Enabled"] = "false"
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            return Create(settings, services =>
            {
                services.AddSingleton(clock);

                // US-018: the GitHub adapter talks to the pretend GitHub instead of api.github.com
                services.AddHttpClient<GitHubWorkItemSource>().ConfigurePrimaryHttpMessageHandler(github.CreateHandler);

                // Make the test sign-in the default, replacing the cookie + Keycloak setup from Program.cs
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                // OpenID Connect must not fetch Keycloak's settings over the network in the tests
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Configuration = FakeKeycloak.Configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(FakeKeycloak.Configuration);
                });
            });
        }

        // The web app with real sign-in against a Keycloak container
        public static TestApp WithKeycloak(string connectionString, string authority, string clientSecret)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                ["Authentication:Keycloak:Authority"] = authority,
                ["Authentication:Keycloak:ClientId"] = TestKeycloak.ClientId,
                ["Authentication:Keycloak:ClientSecret"] = clientSecret
            };

            return Create(settings, _ => { });
        }

        // Overrides the configuration (connection string, Keycloak) and services for the test
        private static TestApp Create(Dictionary<string, string?> settings, Action<IServiceCollection> configureServices)
        {
            var baseFactory = new WebApplicationFactory<Program>();
            var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
                builder.ConfigureTestServices(configureServices);
            });

            return new TestApp(baseFactory, factory);
        }

        public void Dispose()
        {
            Factory.Dispose();
            _base.Dispose();
        }

        // A made-up Keycloak setup, so the app knows where Keycloak's sign-out page "is" without asking it
        private static class FakeKeycloak
        {
            public const string Issuer = "http://keycloak.test/realms/compliance-tracker";

            public static readonly OpenIdConnectConfiguration Configuration = new()
            {
                Issuer = Issuer,
                AuthorizationEndpoint = Issuer + "/protocol/openid-connect/auth",
                TokenEndpoint = Issuer + "/protocol/openid-connect/token",
                EndSessionEndpoint = Issuer + "/protocol/openid-connect/logout"
            };
        }
    }
}
