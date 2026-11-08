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

        // US-014: runs a command inside the PostgreSQL container, e.g. the backup script. Started first if needed.
        public static async Task<DotNet.Testcontainers.Containers.ExecResult> ExecAsync(params string[] command)
        {
            await GetConnectionStringAsync();
            return await _container!.ExecAsync(command);
        }

        // US-014: copies a file into the PostgreSQL container
        public static async Task CopyAsync(byte[] content, string path)
        {
            await GetConnectionStringAsync();
            await _container!.CopyAsync(content, path);
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

        // US-091: the activity log refuses DELETE (a trigger); the reset turns the triggers off for that one table, as its owner.
        // Empties the database before each scenario, but keeps the built-in frameworks (CRA, AI Act)
        // that the migrations add (US-006). TRUNCATE ... CASCADE would delete those too, so the tables
        // are emptied one by one, children before parents. A new table must be added here.
        public static async Task ResetAsync(string connectionString)
        {
            await using var db = CreateContext(connectionString);
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "Invitations";
                DELETE FROM "ApprovalRequests";
                DELETE FROM "OrganisationDocumentFiles";
                DELETE FROM "OrganisationDocumentVersions";
                DELETE FROM "OrganisationDocumentLinks";
                DELETE FROM "ProjectClassifications";
                DELETE FROM "SecurityEventSuggestions";
                DELETE FROM "ExploitedLists";
                DELETE FROM "SecurityEvents";
                DELETE FROM "RiskActionLinks";
                DELETE FROM "RiskActions";
                DELETE FROM "RiskMeasures";
                DELETE FROM "Risks";
                DELETE FROM "MappingSuggestions";
                DELETE FROM "ApplicabilitySuggestions";
                DELETE FROM "AiEvaluations";
                DELETE FROM "Findings";
                DELETE FROM "ScanFiles";
                DELETE FROM "Scans";
                DELETE FROM "ScanUploadTokens";
                DELETE FROM "EvidenceSuggestions";
                DELETE FROM "EvidenceLinkSuggestions";
                DELETE FROM "EvidenceLinks";
                DELETE FROM "EvidenceDocuments";
                DELETE FROM "Evidence";
                DELETE FROM "OrganisationDocuments";
                DELETE FROM "RequirementLinks";
                DELETE FROM "RequirementApplicability";
                ALTER TABLE "ReportSnapshotFiles" DISABLE TRIGGER USER;
                DELETE FROM "ReportSnapshotFiles";
                ALTER TABLE "ReportSnapshotFiles" ENABLE TRIGGER USER;
                ALTER TABLE "ReportSnapshots" DISABLE TRIGGER USER;
                DELETE FROM "ReportSnapshots";
                ALTER TABLE "ReportSnapshots" ENABLE TRIGGER USER;
                ALTER TABLE "ActivityLog" DISABLE TRIGGER USER;
                DELETE FROM "ActivityLog";
                ALTER TABLE "ActivityLog" ENABLE TRIGGER USER;
                DELETE FROM "OrganizationFrameworks";
                DELETE FROM "ProjectFrameworks";
                DELETE FROM "CatalogDocuments";
                DELETE FROM "FrameworkVersions"
                    WHERE "FrameworkId" IN (SELECT "Id" FROM "Frameworks" WHERE "OwnerOrganizationId" IS NOT NULL);
                DELETE FROM "Frameworks" WHERE "OwnerOrganizationId" IS NOT NULL;
                DELETE FROM "WorkItems";
                DELETE FROM "RequirementDocumentFiles";
                DELETE FROM "RequirementDocumentVersions";
                DELETE FROM "RequirementDocuments";
                DELETE FROM "SkippedIssues";
                DELETE FROM "FeedbackScreenshots";
                DELETE FROM "Feedback";
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
