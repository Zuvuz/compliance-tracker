using Microsoft.EntityFrameworkCore;
using ComplianceTracker.Application.Ai;
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
        // ai: the pretend local AI (US-020); without one, a new one
        // exploited: the pretend exploited-vulnerability lists and the operator's switch (US-128); without one, none is on
        public static TestApp WithTestSignIn(string connectionString, TimeProvider clock, IDictionary<string, string?>? overrides = null, FakeGitHub? github = null, FakeAi? ai = null, DatabaseRace? race = null, FakeExploitedLists? exploited = null)
        {
            github ??= new FakeGitHub();
            ai ??= new FakeAi();
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                // Only used when the app redirects to Keycloak on sign-out. There is no real Keycloak in these tests.
                ["Authentication:Keycloak:Authority"] = FakeKeycloak.Issuer,
                ["Authentication:Keycloak:ClientId"] = "compliance-tracker-web",
                ["Authentication:Keycloak:ClientSecret"] = "not-used-in-tests",
                // The tests migrate their own database (TestDatabase); some start the app without one
                ["Database:MigrateOnStartup"] = "false",
                // The tests run the scheduled GitHub check themselves ("the scheduled check runs"), never on a timer
                ["WorkItemSync:Enabled"] = "false",
                // US-063: the feedback writer's token, which the pretend GitHub accepts
                ["Feedback:GitHubToken"] = FakeGitHub.FeedbackToken,
                // US-137: EUR-Lex imports go to the pretend Publications Office (FakeEurLex)
                ["EurLex:Enabled"] = "true"
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            return Create(settings, services =>
            {
                services.AddSingleton(clock);

                // US-139: the scenarios read how many queries a page made
                services.AddSingleton<ComplianceTracker.Infrastructure.Persistence.IQueryCountObserver, QueryCounts>();

                // US-098: the scenarios read the app's log lines
                services.AddSingleton<CapturedLogs>();
                services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(provider => provider.GetRequiredService<CapturedLogs>());

                // US-018: the GitHub adapter talks to the pretend GitHub instead of api.github.com
                services.AddHttpClient<GitHubWorkItemSource>().ConfigurePrimaryHttpMessageHandler(github.CreateHandler);

                // US-063: issues made from feedback go to the pretend GitHub too
                services.AddHttpClient<ComplianceTracker.Infrastructure.Feedback.GitHubFeedbackIssueWriter>()
                    .ConfigurePrimaryHttpMessageHandler(github.CreateHandler);

                // US-137: the EUR-Lex adapter reads the pretend Publications Office
                services.AddHttpClient<ComplianceTracker.Infrastructure.Frameworks.HttpEurLexSource>().ConfigurePrimaryHttpMessageHandler(FakeEurLex.CreateHandler);

                // US-020: the pretend AI replaces the Ollama adapters (the last registration wins)
                services.AddSingleton<IEmbeddingModel>(ai);
                services.AddSingleton<IAiLimits>(ai); // US-105
                services.AddSingleton<IRationaleWriter>(ai);
                services.AddSingleton<IAiModels>(new FakeAiModels(ai)); // US-117

                // US-128: the list adapters read the pretend lists, and a scenario switches them on
                if (exploited is not null)
                {
                    services.AddSingleton<ComplianceTracker.Application.Security.IExploitedListSettings>(exploited);
                    services.AddHttpClient<ComplianceTracker.Infrastructure.Security.KevExploitedList>().ConfigurePrimaryHttpMessageHandler(exploited.CreateHandler);
                    services.AddHttpClient<ComplianceTracker.Infrastructure.Security.EuvdExploitedList>().ConfigurePrimaryHttpMessageHandler(exploited.CreateHandler);
                }

                // US-015: lets a scenario save something at the same moment as the app
                if (race is not null)
                {
                    services.ConfigureDbContext<ComplianceTracker.Infrastructure.Persistence.ComplianceTrackerDbContext>(
                        options => options.AddInterceptors(race.Interceptor));
                }

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

        // US-088: the web app with its real cookie and Keycloak setup (no test sign-in), where CookieSignIn gives the
        // cookie. The clock replaces TimeProvider.System, so a scenario can let time pass between requests.
        public static TestApp WithCookieSignIn(string connectionString, TimeProvider clock, IDictionary<string, string?>? overrides = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                ["Authentication:Keycloak:Authority"] = FakeKeycloak.Issuer,
                ["Authentication:Keycloak:ClientId"] = "compliance-tracker-web",
                ["Authentication:Keycloak:ClientSecret"] = "not-used-in-tests",
                ["Database:MigrateOnStartup"] = "false",
                ["WorkItemSync:Enabled"] = "false"
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            return Create(settings, services =>
            {
                services.AddSingleton(clock);
                services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, CookieSignIn>();

                // A closed page sends the user to the made-up Keycloak, without asking a real one for its settings
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Configuration = FakeKeycloak.Configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(FakeKeycloak.Configuration);
                });
            });
        }

        // The made-up Keycloak's sign-in page, where a closed page sends a signed-out user
        public static string FakeKeycloakSignInPage => FakeKeycloak.Configuration.AuthorizationEndpoint;

        // The web app with real sign-in against a Keycloak container
        public static TestApp WithKeycloak(string connectionString, string authority, string clientSecret)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ComplianceTracker"] = connectionString,
                ["Authentication:Keycloak:Authority"] = authority,
                ["Authentication:Keycloak:ClientId"] = TestKeycloak.ClientId,
                ["Authentication:Keycloak:ClientSecret"] = clientSecret,
                ["Database:MigrateOnStartup"] = "false"
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
