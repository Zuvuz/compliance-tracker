using ComplianceTracker.Web.Diagnostics;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Infrastructure;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Web.Background;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Hubs;
using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

// Start-up of the web app. Two parts:
// 1. Before builder.Build(): register services (what the app can do).
// 2. After builder.Build(): the pipeline (the order every request passes through).

var builder = WebApplication.CreateBuilder(args);

// The DI container checks at start that every service, controller included, can be created (US-029 R5),
// and that no scoped service is used from a singleton
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// MVC: controllers with Razor views.
// The filters turn a broken domain rule into an error on the page (US-029 R4).
// AddControllersAsServices lets the container check the controllers too.
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add<DomainExceptionFilter>();
        options.Filters.Add<DomainErrorFromTempDataFilter>();
    })
    // The domain checks the input (US-029 R4); no page loads the browser-side validation scripts.
    // US-087: without it, the validation summary no longer renders a hidden <li style="display:none">,
    // so the Content-Security-Policy can forbid inline styles.
    .AddViewOptions(options => options.HtmlHelperOptions.ClientValidationEnabled = false)
    .AddControllersAsServices();

// US-098: JSON logs with the request's reference, outside Development
builder.AddOperationalLogging();

// US-097: limits on uploads, other changes and the AI
builder.AddRateLimits();

// Each layer registers its own services (US-029 R2):
// Application = use cases, queries, activity logger and clock; Infrastructure = database and repositories
builder.Services
    .AddApplication()
    .AddInfrastructure();

// US-139: the page a slow query was made for
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ComplianceTracker.Infrastructure.Persistence.ICurrentRequestPath, ComplianceTracker.Web.Diagnostics.HttpRequestPath>();

// Who is signed in: read from the sign-in cookie of the current request (US-029 R1),
// or from the SignalR connection inside a hub call (US-018)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPrincipal>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// US-010: lets pages hide what the member's role does not allow (the use cases check it anyway)
builder.Services.AddScoped<PermissionView>();

// US-018: open project pages update themselves (R7), and GitHub is checked at a fixed interval (R6)
builder.Services.AddSignalR();
builder.Services.AddSingleton<IWorkItemNotifier, SignalRWorkItemNotifier>();
builder.Services
    .AddOptions<WorkItemSyncOptions>()
    .BindConfiguration(WorkItemSyncOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<ScheduledWorkItemSync>();

// US-128: the exploited-vulnerability lists, fetched once a day when the operator switches them on
builder.Services.AddHostedService<ScheduledExploitedListRefresh>();

// US-156: assesses again what needs action after the product changed
builder.Services.AddSingleton<ComplianceTracker.Application.Ai.IReassessmentScopes, ReassessmentScopes>();
builder.Services.AddHostedService<NeedsActionReassessmentWorker>();

// US-020: writes the AI suggestions' rationales in the background
builder.Services.AddSingleton<RationaleWorker>();
builder.Services.AddSingleton<IRationaleQueue>(provider => provider.GetRequiredService<RationaleWorker>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<RationaleWorker>());

// US-064 R7: the product name (Branding:ProductName), shown in the title, the top bar and the footer
builder.Services.AddOptions<BrandingOptions>().BindConfiguration(BrandingOptions.Section);

// The Keycloak settings, checked when the app starts (US-029 R3)
builder.Services
    .AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Sign-in with Keycloak via OpenID Connect (US-004)
builder.Services
    .AddAuthentication(options =>
    {
        // The cookie remembers the signed-in user; OpenID Connect is used when sign-in is missing
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect();

// US-088: the session ends after a while without activity, and after a working day at most
builder.Services.AddOptions<SignInSessionOptions>().BindConfiguration(SignInSessionOptions.Section);
builder.Services
    .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<IOptions<SignInSessionOptions>>((cookie, session) => SessionLifetime.Configure(cookie, session, builder.Environment.IsDevelopment()));

// OpenID Connect is set up from the checked KeycloakOptions.
// That way the integration tests can supply their own Keycloak address and secret.
builder.Services
    .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<IOptions<KeycloakOptions>>((options, keycloakOptions) =>
    {
        // Authority and ClientId are in appsettings.Development.json; the secret only in an environment variable
        var keycloak = keycloakOptions.Value;
        options.Authority = keycloak.Authority;
        options.ClientId = keycloak.ClientId;
        options.ClientSecret = keycloak.ClientSecret;

        options.ResponseType = "code";    // authorization code flow
        options.UsePkce = true;           // ASVS V10.2.1
        options.Scope.Add("email");       // "openid" and "profile" are included by default
        options.SaveTokens = true;        // the ID token is needed for signing out of Keycloak
        options.MapInboundClaims = false; // keep Keycloak's claim names: "sub", "email", "name"
        options.TokenValidationParameters.NameClaimType = "name"; // User.Identity.Name shows the name

        // US-098 R4: a failed sign-in is a security event (only the kind of failure, never a token)
        options.Events.OnRemoteFailure = context =>
        {
            ComplianceTracker.Web.Diagnostics.OperationalLogging.SignInFailed(
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(ComplianceTracker.Web.Diagnostics.OperationalLogging.SecurityCategory),
                context.Failure);
            return Task.CompletedTask;
        };

        // Log every sign-in once Keycloak has confirmed the user (US-004 R6)
        options.Events.OnTokenValidated = async context =>
        {
            // US-132 R2: when the person signed in, so signing a report can ask for a fresh sign-in
            if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity signedIn)
            {
                signedIn.AddClaim(new System.Security.Claims.Claim(ComplianceTracker.Web.Security.HttpContextCurrentUser.SignedInClaim,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            var externalId = context.Principal?.FindFirstValue("sub");
            if (externalId is not null)
            {
                var activity = context.HttpContext.RequestServices.GetRequiredService<UserActivityService>();
                await activity.RecordSignInAsync(externalId, context.HttpContext.RequestAborted);
            }
        };

        if (builder.Environment.IsDevelopment())
        {
            // Locally, Keycloak runs on http, not https
            options.RequireHttpsMetadata = false;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            // Come back from Keycloak through a link (GET) instead of a form,
            // so the cookies are sent even though the app runs on https and Keycloak on http
            options.ResponseMode = OpenIdConnectResponseMode.Query;
        }
    });

// US-093: where to report a vulnerability in the tracker (/.well-known/security.txt)
builder.Services.AddOptions<SecurityContactOptions>().BindConfiguration(SecurityContactOptions.Section);

// US-017: /health for Docker and CI; only says whether the database can be reached
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// Every page requires sign-in, unless it is marked [AllowAnonymous] (US-004 R1)
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;

    // US-010: the tracker's owner, given as a Keycloak realm role
    options.AddPolicy(SystemAdmin.Policy, policy => policy.RequireClaim(SystemAdmin.ClaimType, SystemAdmin.Role));
});

var app = builder.Build();

// US-087: security headers on every response, also static files and error pages
app.UseSecurityHeaders();

// Locally the database is migrated when the app starts (Database:MigrateOnStartup in appsettings.Development.json),
// so new tables from a pull exist before the first request
await DatabaseMigrator.MigrateIfEnabledAsync(app.Services, app.Configuration);

// Outside Development: a friendly error page instead of technical details, and HSTS (always https)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// US-139: how many database queries the request made, for the tests' limits and the debug log
app.Use(async (context, next) =>
{
    await next();
    var queries = context.RequestServices.GetService<ComplianceTracker.Infrastructure.Persistence.RequestQueryCounter>()?.Count ?? 0;
    if (queries > 0)
    {
        context.RequestServices.GetRequiredService<ComplianceTracker.Infrastructure.Persistence.IQueryCountObserver>().Counted(context.Request.Path, queries);
    }
});

app.UseRouting();

app.UseAuthentication();   // who is the user?

// US-098: every log line of a request carries its reference and the user; refused access and errors are watched
app.UseOperationalLogging();

// US-097: after the user is known, so the limits are per user (and the 429s are logged by the line above)
app.UseRateLimiter();
app.UseAuthorization();    // what may the user do?

// CSS, JavaScript and images must be available without sign-in
app.MapStaticAssets().AllowAnonymous();

// US-017: Docker and CI ask this before they send traffic; it answers without sign-in
app.MapHealthChecks("/health").AllowAnonymous();

// US-093: the security contact (RFC 9116), without sign-in
app.MapSecurityTxt();

// URLs map to controller/action/id, e.g. /Frameworks/Versions/{id}. "/" goes to Home/Index.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// US-018 R7: the live updates of the project page's work items
app.MapHub<WorkItemsHub>(WorkItemsHub.Path);

await app.RunAsync();

// Makes Program visible to the integration tests (WebApplicationFactory<Program>)
public partial class Program { }
