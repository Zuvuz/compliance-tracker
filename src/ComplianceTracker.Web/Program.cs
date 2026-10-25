using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;        
using Microsoft.AspNetCore.Authentication.OpenIdConnect;  
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Database, repositories og unit of work (Infrastructure) og use cases (Application)
builder.Services.AddInfrastructure();
builder.Services.AddScoped<OrganizationService>();

// NYT: login med Keycloak via OpenID Connect
builder.Services
    .AddAuthentication(options =>
    {
        // Cookie husker brugeren; OpenID Connect bruges, når login mangler
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        var keycloak = builder.Configuration.GetSection("Authentication:Keycloak");
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

        if (builder.Environment.IsDevelopment())
        {
            // Lokalt kører Keycloak på http, ikke https
            options.RequireHttpsMetadata = false;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.ResponseMode = OpenIdConnectResponseMode.Query;
        }
    });


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

app.UseAuthentication();   
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Gør Program synlig for integrationstestene (WebApplicationFactory<Program>)
public partial class Program { }