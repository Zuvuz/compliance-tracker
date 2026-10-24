using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // Bruges kun af dotnet ef (migrations og database update).
    // Connection string læses fra en miljøvariabel, så den aldrig ligger i repoet.
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
