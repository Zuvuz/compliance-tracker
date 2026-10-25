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
    // Starter den rigtige webapp i hukommelsen (WebApplicationFactory) mod testdatabasen.
    public sealed class TestApp : IDisposable
    {
        private readonly WebApplicationFactory<Program> _base;

        public WebApplicationFactory<Program> Factory { get; }

        private TestApp(WebApplicationFactory<Program> baseFactory, WebApplicationFactory<Program> factory)
        {
            _base = baseFactory;
            Factory = factory;
        }

        // Webappen med test-login i stedet for Keycloak (se TestAuthHandler)
        public static TestApp WithTestSignIn(string connectionString)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                // Bruges kun, når appen sender videre til Keycloak ved log ud. Der er ingen rigtig Keycloak i disse tests.
                ["Authentication:Keycloak:Authority"] = FakeKeycloak.Issuer,
                ["Authentication:Keycloak:ClientId"] = "compliance-tracker-web",
                ["Authentication:Keycloak:ClientSecret"] = "not-used-in-tests"
            };

            return Create(settings, services =>
            {
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                // OpenID Connect skal ikke hente Keycloaks opsætning over netværket i testene
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Configuration = FakeKeycloak.Configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(FakeKeycloak.Configuration);
                });
            });
        }

        // Webappen med rigtigt login mod en Keycloak-container
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
