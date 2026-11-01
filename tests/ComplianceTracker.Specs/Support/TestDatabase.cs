using ComplianceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace ComplianceTracker.Specs.Support
{
    // Starts one PostgreSQL container for the whole test run, the first time an integration test asks for it.
    // Testcontainers makes up the user name and password itself, so there are no credentials in the repo.
    // Requires Docker to be running (locally: Docker Desktop; in CI: GitHub's ubuntu runner).
    public static class TestDatabase
    {
        // Makes sure only one test starts the container
        private static readonly SemaphoreSlim Lock = new(1, 1);
        private static PostgreSqlContainer? _container;
        private static string? _connectionString;

        public static async Task<string> GetConnectionStringAsync()
        {
            await Lock.WaitAsync();
            try
            {
                if (_connectionString is null)
                {
                    _container = new PostgreSqlBuilder("postgres:17").Build();
                    await _container.StartAsync();
                    _connectionString = _container.GetConnectionString();

                    // Runs the migrations in the repo, so the tests also prove that they work
                    await using var db = CreateContext(_connectionString);
                    await db.Database.MigrateAsync();
                }

                return _connectionString;
            }
            finally
            {
                Lock.Release();
            }
        }

        // Interceptors let a test change what happens in the database, e.g. make saving fail (US-016)
        public static ComplianceTrackerDbContext CreateContext(string connectionString, params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<ComplianceTrackerDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptors)
                .Options;

            return new ComplianceTrackerDbContext(options);
        }

        // Empties the database before each scenario, but keeps the built-in frameworks (CRA, AI Act)
        // that the migrations add (US-006). TRUNCATE ... CASCADE would delete those too, so the tables
        // are emptied one by one, children before parents. A new table must be added here.
        public static async Task ResetAsync(string connectionString)
        {
            await using var db = CreateContext(connectionString);
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "RequirementLinks";
                DELETE FROM "ActivityLog";
                DELETE FROM "OrganizationFrameworks";
                DELETE FROM "ProjectFrameworks";
                DELETE FROM "CatalogDocuments";
                DELETE FROM "FrameworkVersions"
                    WHERE "FrameworkId" IN (SELECT "Id" FROM "Frameworks" WHERE "OwnerOrganizationId" IS NOT NULL);
                DELETE FROM "Frameworks" WHERE "OwnerOrganizationId" IS NOT NULL;
                DELETE FROM "WorkItems";
                DELETE FROM "SkippedIssues";
                DELETE FROM "Projects";
                DELETE FROM "Users";
                DELETE FROM "Organizations";
                """);
        }

        public static async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }
}
