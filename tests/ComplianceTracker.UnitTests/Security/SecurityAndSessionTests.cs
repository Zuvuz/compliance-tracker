using System.Security.Claims;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.UnitTests.Security;

// US-152: edge-case unit tests of US-087 (security headers) and US-088 (session timeout). Arrange / Act / Assert.
public class SecurityAndSessionTests
{
    // ---------------------------------------------------------- US-087 Security headers

    [Fact]
    public void The_policy_allows_only_the_app_itself_and_never_inline_code()
    {
        var policy = SecurityHeaders.ContentSecurityPolicy(null);

        Assert.Contains("default-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Contains("object-src 'none'", policy);
        Assert.DoesNotContain("unsafe-inline", policy);
        Assert.DoesNotContain("unsafe-eval", policy);
        Assert.EndsWith("form-action 'self'", policy);
    }

    [Fact]
    public void Forms_may_also_go_to_Keycloak_but_only_to_its_origin()
        => Assert.EndsWith("form-action 'self' https://kc.example:8543", SecurityHeaders.ContentSecurityPolicy("https://kc.example:8543"));

    // A response feature that runs the "on starting" callbacks, as a server does
    private sealed class StartingResponse : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = new();

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _starting)
            {
                await callback(state);
            }
        }
    }

    private static async Task<IHeaderDictionary> HeadersAsync(string authority)
    {
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new KeycloakOptions { Authority = authority, ClientId = "c", ClientSecret = "s" }))
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseSecurityHeaders();
        app.Run(_ => Task.CompletedTask);
        var response = new StartingResponse();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);

        await app.Build()(context);
        await response.StartAsync();
        return context.Response.Headers;
    }

    [Fact]
    public async Task Every_response_gets_the_headers_with_Keycloaks_origin_and_not_its_path()
    {
        var headers = await HeadersAsync("https://keycloak.localhost:8543/realms/compliance-tracker");

        Assert.EndsWith("form-action 'self' https://keycloak.localhost:8543", headers.ContentSecurityPolicy.ToString());
        Assert.Equal(("nosniff", "DENY"), (headers.XContentTypeOptions.ToString(), headers.XFrameOptions.ToString()));
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
        Assert.Equal(SecurityHeaders.PermissionsPolicy, headers["Permissions-Policy"].ToString());
    }

    [Fact]
    public async Task An_authority_that_is_not_an_address_adds_nothing_to_form_action()
        => Assert.EndsWith("form-action 'self'", (await HeadersAsync("not a url")).ContentSecurityPolicy.ToString());

    // ---------------------------------------------------------- US-088 Session timeout

    private sealed class MovableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly AuthenticationScheme Scheme = new(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));

    private static (CookieAuthenticationOptions Options, HttpContext Http, MovableClock Clock) Session()
    {
        var clock = new MovableClock();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(Options.Create(new SignInSessionOptions { IdleMinutes = 30, AbsoluteHours = 8 }));
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        var options = new CookieAuthenticationOptions();
        SessionLifetime.Configure(options, Options.Create(new SignInSessionOptions { IdleMinutes = 30, AbsoluteHours = 8 }), development: false);
        return (options, new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }, clock);
    }

    // A user without "sub", so ending the session logs nothing (that needs the database)
    private static readonly ClaimsPrincipal Anna = new(new ClaimsIdentity([new Claim("name", "Anna")], "test"));

    private static async Task<AuthenticationProperties> SignInAsync(CookieAuthenticationOptions options, HttpContext http)
    {
        var properties = new AuthenticationProperties();
        await options.Events.OnSigningIn(new CookieSigningInContext(http, Scheme, options, Anna, properties, new CookieOptions()));
        return properties;
    }

    private static async Task<CookieValidatePrincipalContext> ValidateAsync(CookieAuthenticationOptions options, HttpContext http, AuthenticationProperties properties)
    {
        var context = new CookieValidatePrincipalContext(http, Scheme, options, new AuthenticationTicket(Anna, properties, Scheme.Name));
        await options.Events.OnValidatePrincipal(context);
        return context;
    }

    [Fact]
    public void The_cookie_is_http_only_lax_secure_and_lasts_the_absolute_time_without_sliding()
    {
        var (options, _, _) = Session();

        Assert.Equal((true, SameSiteMode.Lax, CookieSecurePolicy.Always, TimeSpan.FromHours(8), false),
            (options.Cookie.HttpOnly, options.Cookie.SameSite, options.Cookie.SecurePolicy, options.ExpireTimeSpan, options.SlidingExpiration));
    }

    [Fact]
    public void Locally_the_cookie_follows_the_request_so_http_works()
    {
        var options = new CookieAuthenticationOptions();

        SessionLifetime.Configure(options, Options.Create(new SignInSessionOptions()), development: true);

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
    }

    [Theory]
    [InlineData(30 * 60, true)]       // exactly the idle time: still signed in
    [InlineData(30 * 60 + 1, false)]  // one second more: signed out
    public async Task The_session_ends_only_after_more_than_the_idle_time(int seconds, bool signedIn)
    {
        var (options, http, clock) = Session();
        var properties = await SignInAsync(options, http);
        clock.Now = clock.Now.AddSeconds(seconds);

        var context = await ValidateAsync(options, http, properties);

        Assert.Equal(signedIn, context.Principal is not null);
    }

    [Fact]
    public async Task Activity_keeps_the_session_but_not_past_the_absolute_time()
    {
        var (options, http, clock) = Session();
        var properties = await SignInAsync(options, http);

        // A request every 20 minutes for 8 hours keeps it alive …
        for (var i = 0; i < 24; i++)
        {
            clock.Now = clock.Now.AddMinutes(20);
            Assert.NotNull((await ValidateAsync(options, http, properties)).Principal);
        }

        // … but one second past 8 hours ends it, however active the user is
        clock.Now = clock.Now.AddSeconds(1);
        Assert.Null((await ValidateAsync(options, http, properties)).Principal);
    }

    [Fact]
    public async Task Last_seen_is_written_again_at_most_once_a_minute()
    {
        var (options, http, clock) = Session();
        var properties = await SignInAsync(options, http);

        clock.Now = clock.Now.AddSeconds(59);
        Assert.False((await ValidateAsync(options, http, properties)).ShouldRenew);

        clock.Now = clock.Now.AddSeconds(1);
        Assert.True((await ValidateAsync(options, http, properties)).ShouldRenew);
    }

    [Fact]
    public async Task A_cookie_from_before_the_timeout_gets_its_times_now_and_is_kept()
    {
        var (options, http, _) = Session();

        var context = await ValidateAsync(options, http, new AuthenticationProperties());

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.True(context.Properties.Items.ContainsKey(".session_started"));
    }

    [Fact]
    public async Task A_time_that_cannot_be_read_counts_as_missing_not_as_expired()
    {
        var (options, http, _) = Session();
        var properties = new AuthenticationProperties();
        properties.Items[".session_started"] = "yesterday-ish";
        properties.Items[".session_seen"] = "";

        var context = await ValidateAsync(options, http, properties);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
    }
}
