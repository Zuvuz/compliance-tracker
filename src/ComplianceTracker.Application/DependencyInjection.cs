using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Application.WorkItems;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ComplianceTracker.Application
{
    // Registers the Application layer's use cases and queries (US-029 R2).
    // The web app calls AddApplication() once in Program.cs, next to AddInfrastructure().
    // A new use case or query only has to be added here, not in the web app.
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // The clock (US-016). TryAdd, so tests can register a fixed clock first.
            services.TryAddSingleton(TimeProvider.System);

            // Scoped = one instance per HTTP request, sharing the request's DbContext through the repositories
            services.AddScoped<CurrentMember>();
            services.AddScoped<IActivityLogger, ActivityLogger>();

            // Use cases (change data)
            services.AddScoped<OrganizationService>();
            services.AddScoped<UserActivityService>();
            services.AddScoped<FrameworkService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<WorkItemSyncService>();

            // Queries (read data for the pages)
            services.AddScoped<OrganizationQueries>();
            services.AddScoped<FrameworkQueries>();
            services.AddScoped<ProjectQueries>();
            services.AddScoped<WorkItemQueries>();

            return services;
        }
    }
}
