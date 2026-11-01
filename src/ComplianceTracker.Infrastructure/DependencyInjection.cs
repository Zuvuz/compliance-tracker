using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Infrastructure.Frameworks;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using ComplianceTracker.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using ComplianceTracker.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Infrastructure
{
    // Connects the interfaces from Application to their database implementations.
    // The web app calls AddInfrastructure() once in Program.cs.
    public static class DependencyInjection
    {
        public const string ConnectionStringName = "ComplianceTracker";

        // US-018: GitHub's API. The integration tests replace the HTTP handler with a fake GitHub.
        public const string GitHubApiAddress = "https://api.github.com/";

        // Registers the database, the repositories and the unit of work.
        // The connection string comes from configuration (ConnectionStrings:ComplianceTracker), e.g. the
        // environment variable ConnectionStrings__ComplianceTracker. Never from appsettings.json in the repo.
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddDbContext<ComplianceTrackerDbContext>((serviceProvider, options) =>
            {
                // Read when the DbContext is first used, so the integration tests can supply their own database
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{ConnectionStringName}' is missing. " +
                        $"Set the environment variable 'ConnectionStrings__{ConnectionStringName}'.");

                options.UseNpgsql(connectionString);
            });

            // "Scoped" = one instance per HTTP request. The repositories and the unit of work
            // therefore share the same DbContext and save in the same transaction.
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IFrameworkRepository, FrameworkRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();

            // US-007: reads requirements from PDF text. Holds no state, so one instance is enough.
            services.AddSingleton<IRequirementExtractor, PdfRequirementExtractor>();

            // US-018: the GitHub adapter (B11). A typed HttpClient, so connections are reused safely.
            // GitHub requires a User-Agent; the API version header keeps the JSON format stable.
            services.AddScoped<IWorkItemRepository, WorkItemRepository>();
            services.AddScoped<IRequirementLinkRepository, RequirementLinkRepository>();
            services.AddHttpClient<GitHubWorkItemSource>(client =>
            {
                client.BaseAddress = new Uri(GitHubApiAddress);
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("compliance-tracker");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            });
            services.AddTransient<IWorkItemSource>(provider => provider.GetRequiredService<GitHubWorkItemSource>());

            // US-031: encryption of stored tokens. The keys are kept in the database (DataProtectionKeys).
            // The same keys protect the sign-in cookie and antiforgery tokens, so they also survive restarts.
            services.AddDataProtection()
                .SetApplicationName("compliance-tracker")
                .PersistKeysToDbContext<ComplianceTrackerDbContext>();
            services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

            return services;
        }
    }
}
