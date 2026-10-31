using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // Brings the database up to date with the code when the app starts, so a developer never meets
    // "relation ... does not exist" after pulling new migrations.
    // Only when the setting Database:MigrateOnStartup is true (appsettings.Development.json).
    // Off in production, where a migration should be a deliberate step, and in the integration tests,
    // which migrate their own test database.
    public static class DatabaseMigrator
    {
        public const string Setting = "Database:MigrateOnStartup";

        public static async Task MigrateIfEnabledAsync(IServiceProvider services, IConfiguration configuration)
        {
            if (!configuration.GetValue<bool>(Setting))
            {
                return;
            }

            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ComplianceTrackerDbContext>().Database.MigrateAsync();
        }
    }
}
