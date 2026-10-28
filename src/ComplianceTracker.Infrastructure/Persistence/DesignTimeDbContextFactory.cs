using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // Only used by the dotnet ef tool (migrations add, database update), not by the running app.
    // The connection string is read from an environment variable, so it never ends up in the repo.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ComplianceTrackerDbContext>
    {
        public const string ConnectionStringVariable = "ConnectionStrings__ComplianceTracker";

        public ComplianceTrackerDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)
                ?? throw new InvalidOperationException(
                    $"Set the environment variable '{ConnectionStringVariable}' to a PostgreSQL connection string.");

            var options = new DbContextOptionsBuilder<ComplianceTrackerDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new ComplianceTrackerDbContext(options);
        }
    }
}
