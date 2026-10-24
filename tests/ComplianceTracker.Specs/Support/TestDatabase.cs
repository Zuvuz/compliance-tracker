using ComplianceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ComplianceTracker.Specs.Support
{
    // Starter én PostgreSQL-container til hele testkørslen, første gang en integrationstest beder om den.
    // Testcontainers finder selv på brugernavn og kodeord, så der ligger ingen credentials i repoet.
    // Kræver at Docker kører (lokalt: Docker Desktop, i CI: GitHub's ubuntu-runner).
    public static class TestDatabase
    {
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

                    // Kører den migration, der ligger i repoet, så testen også beviser, at den virker
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

        public static ComplianceTrackerDbContext CreateContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<ComplianceTrackerDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new ComplianceTrackerDbContext(options);
        }

        public static async Task ResetAsync(string connectionString)
        {
            await using var db = CreateContext(connectionString);
            await db.Database.ExecuteSqlRawAsync(
                """TRUNCATE "ActivityLog", "Users", "Organizations" RESTART IDENTITY CASCADE""");
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
