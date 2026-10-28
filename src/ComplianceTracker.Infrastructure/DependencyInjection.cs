using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
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

            return services;
        }
    }
}
