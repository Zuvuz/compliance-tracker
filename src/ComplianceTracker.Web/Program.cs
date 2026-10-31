using ComplianceTracker.Application;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Infrastructure;
using ComplianceTracker.Web.Filters;
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
    .AddControllersAsServices();

// Each layer registers its own services (US-029 R2):
// Application = use cases, queries, activity logger and clock; Infrastructure = database and repositories
builder.Services
    .AddApplication()
    .AddInfrastructure();

// Who is signed in: read from the sign-in cookie of the current request (US-029 R1)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

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

        // Log every sign-in once Keycloak has confirmed the user (US-004 R6)
        options.Events.OnTokenValidated = async context =>
        {
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

// Every page requires sign-in, unless it is marked [AllowAnonymous] (US-004 R1)
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

// Outside Development: a friendly error page instead of technical details, and HSTS (always https)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // who is the user?
app.UseAuthorization();    // what may the user do?

// CSS, JavaScript and images must be available without sign-in
app.MapStaticAssets().AllowAnonymous();

// URLs map to controller/action/id, e.g. /Frameworks/Versions/{id}. "/" goes to Home/Index.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Makes Program visible to the integration tests (WebApplicationFactory<Program>)
public partial class Program { }
