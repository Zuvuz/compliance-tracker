using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-014: runs docker/backup-db.sh and docker/restore-db.sh, the scripts the backup service uses, inside the
    // test database's PostgreSQL container, and compares the restored database with the original through EF Core.
    [Binding]
    public class BackupAndRestoreStepDefinitions
    {
        private const string Folder = "/backups";
        private const string RestoredDatabase = "restored";

        private readonly WebScenario _web;
        private readonly DatabaseScenario _database;
        private DotNet.Testcontainers.Containers.ExecResult _lastRun;

        public BackupAndRestoreStepDefinitions(WebScenario web, DatabaseScenario database)
        {
            _web = web;
            _database = database;
        }

        // Every scenario starts with no backups and the scripts copied in
        [BeforeScenario("US-014")]
        public async Task CopyTheScripts()
        {
            await TestDatabase.ExecAsync("rm", "-rf", Folder, "/scripts");
            foreach (var script in new[] { "backup-db.sh", "restore-db.sh" })
            {
                var content = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "scripts", script));
                await TestDatabase.CopyAsync(content, $"/scripts/{script}");
            }
        }

        // ---------------------------------------------------------- Given

        [Given("there are backups from {int} and {int} days ago")]
        public async Task GivenThereAreOldBackups(int first, int second)
        {
            foreach (var days in new[] { first, second })
            {
                var file = $"{Folder}/{Database}_$(date -d '{days} days ago' +%F).dump";
                await RunAsync($"mkdir -p {Folder} && echo old > {file} && touch -d '{days} days ago' {file}");
            }
        }

        // ---------------------------------------------------------- When

        [When("the database is backed up")]
        public async Task WhenTheDatabaseIsBackedUp()
        {
            await RunAsync($"{Variables(Database)} sh /scripts/backup-db.sh {Folder}");
            Assert.True(_lastRun.ExitCode == 0, $"The backup failed: {_lastRun.Stderr}");
        }

        [When("the backup is restored into an empty database")]
        public async Task WhenTheBackupIsRestored()
        {
            await CreateEmptyDatabaseAsync();
            await RunAsync($"{Variables(RestoredDatabase)} sh /scripts/restore-db.sh {Folder}/{Database}_$(date +%F).dump");
            Assert.True(_lastRun.ExitCode == 0, $"The restore failed: {_lastRun.Stderr}");
        }

        [When("a file that is not a backup is restored into an empty database")]
        public async Task WhenAFileThatIsNotABackupIsRestored()
        {
            await CreateEmptyDatabaseAsync();
            await RunAsync($"mkdir -p {Folder} && echo 'not a backup' > {Folder}/garbage.dump");
            await RunAsync($"{Variables(RestoredDatabase)} sh /scripts/restore-db.sh {Folder}/garbage.dump");
        }

        // ---------------------------------------------------------- Then

        // R6: the organisation, the uploaded PDF (byte for byte) and every log entry
        [Then("the restored database has {string}, the framework PDF and the log entries")]
        public async Task ThenTheRestoredDatabaseHasEverything(string organisation)
        {
            await using var original = _web.CreateDbContext();
            await using var restored = TestDatabase.CreateContext(RestoredConnectionString());

            Assert.True(await restored.Organizations.AnyAsync(o => o.Name == organisation), $"'{organisation}' is not in the restored database.");

            var originalPdfs = await original.CatalogDocuments.OrderBy(d => d.FrameworkVersionId).Select(d => d.Content).ToListAsync();
            var restoredPdfs = await restored.CatalogDocuments.OrderBy(d => d.FrameworkVersionId).Select(d => d.Content).ToListAsync();
            Assert.NotEmpty(originalPdfs);
            Assert.Equal(originalPdfs, restoredPdfs);

            var originalLog = await original.ActivityLog.CountAsync();
            Assert.True(originalLog > 0);
            Assert.Equal(originalLog, await restored.ActivityLog.CountAsync());

            // US-091 R6: the restored log keeps its chain of fingerprints
            var restoredLog = await restored.ActivityLog.OrderBy(e => EF.Property<long>(e, "Id")).ToListAsync();
            Assert.Equal(
                await original.ActivityLog.OrderBy(e => EF.Property<long>(e, "Id")).Select(e => e.Hash).ToListAsync(),
                restoredLog.Select(e => e.Hash).ToList());
            Assert.All(restoredLog.GroupBy(e => e.OrganizationId), organization =>
            {
                var check = Domain.ActivityLog.ActivityLogChain.Verify(organization);
                Assert.True(check.IsIntact && check.WithoutFingerprint == 0, $"The restored chain is broken at {check.FirstBroken?.Action}.");
            });
            Assert.Equal(
                (await original.Organizations.SingleAsync(o => o.Name == organisation)).Users.Select(u => u.Name).Order(),
                (await restored.Organizations.SingleAsync(o => o.Name == organisation)).Users.Select(u => u.Name).Order());
        }

        [Then("there are backups from today and from {int} days ago")]
        public async Task ThenThereAreBackupsFrom(int days)
        {
            var files = await BackupFilesAsync();
            Assert.Contains(await FileNameAsync(0), files);
            Assert.Contains(await FileNameAsync(days), files);
        }

        [Then("the backup from {int} days ago is deleted")]
        public async Task ThenTheBackupIsDeleted(int days)
            => Assert.DoesNotContain(await FileNameAsync(days), await BackupFilesAsync());

        [Then("the restore fails")]
        public void ThenTheRestoreFails()
            => Assert.NotEqual(0, _lastRun.ExitCode);

        [Then("the empty database is still empty")]
        public async Task ThenTheEmptyDatabaseIsStillEmpty()
        {
            var result = await TestDatabase.ExecAsync("psql", "-U", User, "-d", RestoredDatabase, "-tAc",
                "select count(*) from information_schema.tables where table_schema = 'public'");
            Assert.Equal("0", result.Stdout.Trim());
        }

        // ---------------------------------------------------------- Helpers

        private NpgsqlConnectionStringBuilder Connection => new(_database.ConnectionString);

        private string Database => Connection.Database!;

        private string User => Connection.Username!;

        // The standard PostgreSQL variables the scripts read, as in the backup service
        private string Variables(string database)
            => $"PGHOST=localhost PGUSER='{User}' PGPASSWORD='{Connection.Password}' PGDATABASE='{database}'";

        private async Task RunAsync(string command)
            => _lastRun = await TestDatabase.ExecAsync("sh", "-c", command);

        private async Task CreateEmptyDatabaseAsync()
        {
            await TestDatabase.ExecAsync("psql", "-U", User, "-d", Database, "-c", $"DROP DATABASE IF EXISTS {RestoredDatabase} WITH (FORCE)");
            var created = await TestDatabase.ExecAsync("psql", "-U", User, "-d", Database, "-c", $"CREATE DATABASE {RestoredDatabase}");
            Assert.True(created.ExitCode == 0, created.Stderr);
        }

        private string RestoredConnectionString()
            => new NpgsqlConnectionStringBuilder(_database.ConnectionString) { Database = RestoredDatabase }.ConnectionString;

        private async Task<string> FileNameAsync(int daysAgo)
            => $"{Database}_{(await TestDatabase.ExecAsync("date", "-d", $"{daysAgo} days ago", "+%F")).Stdout.Trim()}.dump";

        private async Task<IReadOnlyList<string>> BackupFilesAsync()
            => (await TestDatabase.ExecAsync("ls", Folder)).Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
