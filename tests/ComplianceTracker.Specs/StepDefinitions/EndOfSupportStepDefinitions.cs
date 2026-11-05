using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-074: the phase form #change-phase (lastPlacedOnMarket, keepUntil) and the header #project-retention
    [Binding]
    public class EndOfSupportStepDefinitions
    {
        private readonly WebScenario _web;

        public EndOfSupportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I end the support of {string}, last placed on the market {string}")]
        [When("I end the support of {string}, last placed on the market {string}")]
        public Task WhenIEndTheSupport(string project, string lastPlaced)
            => EndAsync(project, lastPlaced, null);

        [When("I end the support of {string}, last placed on the market {string}, and keep it until {string}")]
        public Task WhenIEndTheSupportAndKeep(string project, string lastPlaced, string keepUntil)
            => EndAsync(project, lastPlaced, keepUntil);

        [Then("the page of {string} says {string}")]
        public async Task ThenThePageSays(string project, string text)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.Equal(text, _web.Page!.QuerySelector("#project-retention")?.TextContent.Trim());
        }

        // The phase form on the Overview tab, like a user
        private async Task EndAsync(string project, string lastPlaced, string? keepUntil)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}");
            Assert.NotNull(_web.Page!.QuerySelector("#change-phase input[name=lastPlacedOnMarket]"));

            var fields = new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["phase"] = "EndOfSupport",
                ["lastPlacedOnMarket"] = lastPlaced
            };
            if (keepUntil is not null)
            {
                fields["keepUntil"] = keepUntil;
            }

            await _web.PostFormAsync("/Projects/ChangePhase", fields);
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
