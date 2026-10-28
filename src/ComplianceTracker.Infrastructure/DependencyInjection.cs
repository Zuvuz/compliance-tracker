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
    public static class DependencyInjection
    {
        public const string ConnectionStringName = "ComplianceTracker";

        // Registrerer database, repositories og unit of work.
        // Connection string hentes fra konfigurationen (ConnectionStrings:ComplianceTracker), fx fra
        // miljøvariablen ConnectionStrings__ComplianceTracker eller user secrets. Aldrig fra appsettings.json i repoet.
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddDbContext<ComplianceTrackerDbContext>((serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{ConnectionStringName}' is missing. " +
                        $"Set the environment variable 'ConnectionStrings__{ConnectionStringName}'.");

                options.UseNpgsql(connectionString);
            });

            // Én DbContext pr. HTTP-request, delt af repositories og unit of work,
            // så de gemmer i samme transaktion.
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IFrameworkRepository, FrameworkRepository>();

            return services;
        }
    }
}
