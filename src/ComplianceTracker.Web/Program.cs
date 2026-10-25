using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Database, repositories og unit of work (Infrastructure) og use cases (Application)
builder.Services.AddInfrastructure();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<UserActivityService>();

// Login med Keycloak via OpenID Connect (US-004)
builder.Services
    .AddAuthentication(options =>
    {
        // Cookie husker brugeren; OpenID Connect bruges, når login mangler
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect();

// Indstillingerne læses fra konfigurationen, når login bruges første gang.
// På den måde kan integrationstestene give deres egen Keycloak-adresse og secret.
builder.Services
    .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var keycloak = configuration.GetSection("Authentication:Keycloak");
        options.Authority = keycloak["Authority"];
        options.ClientId = keycloak["ClientId"];
        options.ClientSecret = keycloak["ClientSecret"]
            ?? throw new InvalidOperationException(
                "Set the environment variable Authentication__Keycloak__ClientSecret.");

        options.ResponseType = "code";    // authorization code flow
        options.UsePkce = true;           // ASVS V10.2.1
        options.Scope.Add("email");       // "openid" og "profile" er med som standard
        options.SaveTokens = true;        // bruges ved log ud
        options.MapInboundClaims = false; // behold Keycloaks navne: "sub", "email", "name"
        options.TokenValidationParameters.NameClaimType = "name";

        // Log hvert login, når Keycloak har bekræftet brugeren (R6)
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
            // Lokalt kører Keycloak på http, ikke https
            options.RequireHttpsMetadata = false;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            // Tilbage fra Keycloak via et link (GET) i stedet for en formular,
            // så cookies kommer med, selvom appen kører https og Keycloak http
            options.ResponseMode = OpenIdConnectResponseMode.Query;
        }
    });

// Alle sider kræver login, medmindre de er markeret [AllowAnonymous] (R1)
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // hvem er brugeren?
app.UseAuthorization();    // hvad må brugeren?

// CSS, JavaScript og billeder skal kunne hentes uden login
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Gør Program synlig for integrationstestene (WebApplicationFactory<Program>)
public partial class Program { }
