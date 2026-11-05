using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-015: two saves of the same CVR at the same moment, through the web app and PostgreSQL.
    // DatabaseRace saves "Bo"'s organisation just before the app inserts Anna's, after the app's own CVR check.
    [Binding]
    public class SameCvrAtTheSameTimeStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly DatabaseRace _race;

        public SameCvrAtTheSameTimeStepDefinitions(WebScenario web, DatabaseRace race)
        {
            _web = web;
            _race = race;
        }

        [Given("another organisation with CVR {string} is saved while I am creating mine")]
        public void GivenAnotherOrganisationIsSavedAtTheSameTime(string cvr)
            => _race.BeforeInsertInto("Organizations", () => _web.CreateOrganisationAsync("Bo", "Beta ApS", cvr));

        [Then("there is exactly one organisation with CVR {string}")]
        public async Task ThenThereIsExactlyOneOrganisation(string cvr)
        {
            await using var db = _web.CreateDbContext();
            var organisation = await db.Organizations.SingleAsync(o => o.Cvr == cvr);
            Assert.Equal("Beta ApS", organisation.Name);
        }

        [Then("there is exactly one {string} log entry")]
        public async Task ThenThereIsExactlyOneLogEntry(string action)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<ActivityAction>(action);
            Assert.Equal(1, await db.ActivityLog.CountAsync(e => e.Action == expected));
        }
    }
}
