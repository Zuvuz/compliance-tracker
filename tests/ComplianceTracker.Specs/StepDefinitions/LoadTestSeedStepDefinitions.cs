using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.LoadSeed;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-138: tests/load/seed against the scenario's database; the users get the test sign-in's ids ("<name>-id")
    [Binding]
    public class LoadTestSeedStepDefinitions
    {
        private readonly WebScenario _web;
        private string? _refused;

        public LoadTestSeedStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("the load test's data is seeded for {string}, {string} and {string}")]
        public Task WhenSeededForThree(string a, string b, string c) => SeedAsync(a, b, c);

        [When("the load test's data is seeded for {string}")]
        public Task WhenSeededForOne(string a) => SeedAsync(a);

        [Then("the organisation {string} has {int} projects, {int} stories, {int} pieces of evidence and {int} risks")]
        public async Task ThenTheOrganisationHas(string name, int projects, int stories, int evidence, int risks)
        {
            await using var db = _web.CreateDbContext();
            var organization = await db.Organizations.SingleAsync(o => o.Name == name);
            var ids = await db.Projects.Where(p => p.OrganizationId == organization.Id).Select(p => p.Id).ToListAsync();
            Assert.Equal(projects, ids.Count);
            Assert.Equal(stories, await db.WorkItems.CountAsync(w => ids.Contains(w.ProjectId)));
            Assert.Equal(evidence, await db.Evidence.CountAsync(e => ids.Contains(e.ProjectId)));
            Assert.Equal(risks, await db.Risks.CountAsync(r => ids.Contains(r.ProjectId)));
        }

        [Then("the organisation's activity log has {int} entries and its chain is intact")]
        public async Task ThenTheLogIsIntact(int count)
        {
            await using var db = _web.CreateDbContext();
            var entries = await db.ActivityLog.OrderBy(e => EF.Property<long>(e, "Id")).ToListAsync();
            Assert.Equal(count, entries.Count);
            Assert.True(ActivityLogChain.Verify(entries).IsIntact);
        }

        [Then("{string} sees the project {string} on the projects page")]
        public async Task ThenSeesTheProject(string user, string project)
        {
            using var client = _web.CreateClient(signedInAs: user);
            var page = await WebScenario.ParseAsync(await client.GetAsync("/Projects"));
            Assert.Contains(project, page.Body!.TextContent, StringComparison.Ordinal);
        }

        [Then("the seed is refused with {string}")]
        public void ThenTheSeedIsRefused(string message) => Assert.Equal(message, _refused);

        private async Task SeedAsync(params string[] names)
        {
            await using var db = _web.CreateDbContext();
            try
            {
                await LoadSeed.LoadSeed.RunAsync(db, names.Select(n => new LoadUser($"{n.ToLowerInvariant()}-id", n, $"{n.ToLowerInvariant()}@load.test")).ToList(), DateTime.UtcNow);
            }
            catch (InvalidOperationException error)
            {
                _refused = error.Message;
            }
        }
    }
}
