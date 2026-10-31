using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

// Start-up of the web app. Two parts:
// 1. Before builder.Build(): register services (what the app can do).
// 2. After builder.Build(): the pipeline (the order every request passes through).

var builder = WebApplication.CreateBuilder(args);

// MVC: controllers with Razor views
builder.Services.AddControllersWithViews();

// Database, repositories and unit of work (Infrastructure) and the use cases (Application).
// Scoped = one instance per HTTP request.
builder.Services.AddInfrastructure();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddScoped<FrameworkService>();
builder.Services.AddScoped<ProjectService>();

// The clock the services read the time from (US-016). The tests replace it with a fixed time.
builder.Services.AddSingleton(TimeProvider.System);

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

// The OpenID Connect settings are read from configuration the first time sign-in is used.
// That way the integration tests can supply their own Keycloak address and secret.
builder.Services
    .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        // Authority and ClientId are in appsettings.Development.json; the secret only in an environment variable
        var keycloak = configuration.GetSection("Authentication:Keycloak");
        options.Authority = keycloak["Authority"];
        options.ClientId = keycloak["ClientId"];
        options.ClientSecret = keycloak["ClientSecret"]
            ?? throw new InvalidOperationException(
                "Set the environment variable Authentication__Keycloak__ClientSecret.");

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
