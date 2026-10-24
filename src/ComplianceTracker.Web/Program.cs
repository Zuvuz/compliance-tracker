using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Database, repositories og unit of work (Infrastructure) og use cases (Application)
builder.Services.AddInfrastructure();
builder.Services.AddScoped<OrganizationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Gør Program synlig for integrationstestene (WebApplicationFactory<Program>)
public partial class Program { }
