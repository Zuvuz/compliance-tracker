using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-091: the triggers on "ActivityLog" and the page /LogCheck (Views/LogCheck/Index.cshtml: #log-check-result with
    // data-intact). A change "around the tracker" turns the triggers off first, as an owner of the table could.
    [Binding]
    public class ActivityLogProtectionStepDefinitions
    {
        private const string SecondEntry = """(SELECT "Id" FROM "ActivityLog" ORDER BY "Id" OFFSET 1 LIMIT 1)""";

        private readonly WebScenario _web;
        private PostgresException? _refusal;
        private string? _brokenEntryTime;

        public ActivityLogProtectionStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("the second log entry has been changed around the tracker")]
        public async Task GivenTheSecondEntryHasBeenChanged()
        {
            _brokenEntryTime = await TimeOfEntryAsync(1);
            await AroundTheTriggersAsync($"""UPDATE "ActivityLog" SET "Details" = 'Changed afterwards' WHERE "Id" = {SecondEntry}""");
        }

        // The entry after it is then the first that does not match
        [Given("the second log entry has been removed around the tracker")]
        public async Task GivenTheSecondEntryHasBeenRemoved()
        {
            _brokenEntryTime = await TimeOfEntryAsync(2);
            await AroundTheTriggersAsync($"""DELETE FROM "ActivityLog" WHERE "Id" = {SecondEntry}""");
        }

        // ---------------------------------------------------------- When

        [When("someone changes a log entry directly in the database")]
        public Task WhenSomeoneChangesAnEntry()
            => TryAsync("""UPDATE "ActivityLog" SET "Details" = 'Changed afterwards'""");

        [When("someone deletes a log entry directly in the database")]
        public Task WhenSomeoneDeletesAnEntry()
            => TryAsync("""DELETE FROM "ActivityLog" """);

        [When("I check the activity log")]
        public Task WhenICheckTheLog()
            => _web.OpenAsync("/LogCheck");

        [When("{string} checks the activity log")]
        public async Task WhenOtherChecksTheLog(string user)
        {
            using var client = _web.CreateClient(signedInAs: user, followRedirects: false);
            _web.Response = await client.GetAsync("/LogCheck");
            _web.Page = _web.Response.IsSuccessStatusCode ? await WebScenario.ParseAsync(_web.Response) : null;
        }

        // ---------------------------------------------------------- Then

        [Then("the database refuses with {string}")]
        public async Task ThenTheDatabaseRefuses(string message)
        {
            Assert.NotNull(_refusal);
            Assert.Contains(message, _refusal!.MessageText);
            await using var db = _web.CreateDbContext();
            Assert.False(await db.ActivityLog.AnyAsync(e => e.Details == "Changed afterwards"));
            Assert.True(await db.ActivityLog.AnyAsync());
        }

        [Then("the log is intact")]
        public void ThenTheLogIsIntact()
            => Assert.Equal("true", Result().GetAttribute("data-intact"));

        [Then("every entry has a fingerprint")]
        public async Task ThenEveryEntryHasAFingerprint()
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.ActivityLog.AnyAsync(e => e.Hash == null));
            Assert.Contains($"({await db.ActivityLog.CountAsync()} entries)", Result().TextContent);
        }

        [Then("the log is broken at the second entry")]
        public void ThenTheLogIsBrokenAtTheSecondEntry()
        {
            Assert.Equal("false", Result().GetAttribute("data-intact"));
            Assert.Contains(_brokenEntryTime!, _web.Page!.QuerySelector("#log-check-broken")!.TextContent);
            Assert.Contains("1 entries before it are intact", Result().TextContent);
        }

        // ---------------------------------------------------------- Helpers

        private AngleSharp.Dom.IElement Result()
            => _web.Page?.QuerySelector("#log-check-result")
               ?? throw new Xunit.Sdk.XunitException($"No check result on the page ({(int?)_web.Response?.StatusCode}).");

        // The time of the entry at the position (0 is the first), as the page shows it
        private async Task<string> TimeOfEntryAsync(int position)
        {
            await using var db = _web.CreateDbContext();
            var entry = await db.ActivityLog.OrderBy(e => EF.Property<long>(e, "Id")).Skip(position).FirstAsync();
            return DateTime.SpecifyKind(entry.OccurredAt, DateTimeKind.Utc).ToLocalTime().ToString("dd-MM-yyyy HH:mm");
        }

        private async Task TryAsync(string sql)
        {
            await using var db = _web.CreateDbContext();
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            catch (PostgresException e)
            {
                _refusal = e;
            }
        }

        private async Task AroundTheTriggersAsync(string sql)
        {
            await using var db = _web.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync($"""
                ALTER TABLE "ActivityLog" DISABLE TRIGGER USER;
                {sql};
                ALTER TABLE "ActivityLog" ENABLE TRIGGER USER;
                """);
        }
    }
}
