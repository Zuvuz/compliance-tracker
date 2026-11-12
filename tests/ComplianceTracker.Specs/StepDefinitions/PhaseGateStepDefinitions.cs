using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-071, US-072: the gates between lifecycle phases, through the web app like a user.
    // UI contract: the Overview's #phase-gate[data-name] with li.gate-check[data-state] holding .gate-check-text, and the
    // phase form #change-phase (phase, gateReason).
    [Binding]
    public class PhaseGateStepDefinitions
    {
        private readonly WebScenario _web;

        public PhaseGateStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I move {string} to {string} without a reason")]
        public Task WhenIMoveWithoutAReason(string project, string phase) => MoveAsync(project, phase, null);

        [Given("I move {string} to {string} with the reason {string}")]
        [When("I move {string} to {string} with the reason {string}")]
        public Task WhenIMoveWithTheReason(string project, string phase, string reason) => MoveAsync(project, phase, reason);

        // US-072: a release sends the support end date in the same step (US-025 R1)
        [When("I release {string} with support until {string} and no gate reason")]
        public Task WhenIReleaseWithoutAReason(string project, string date) => MoveAsync(project, "Released", null, date);

        [When("I release {string} with support until {string} and the gate reason {string}")]
        public Task WhenIReleaseWithTheReason(string project, string date, string reason) => MoveAsync(project, "Released", reason, date);

        [Given("the gate {string} shows {string} as {string}")]
        [Then("the gate {string} shows {string} as {string}")]
        public void ThenTheGateShows(string gate, string text, string state)
        {
            var box = _web.Page!.QuerySelectorAll("#phase-gate").FirstOrDefault(g => g.GetAttribute("data-name") == gate)
                ?? throw new Xunit.Sdk.XunitException($"The Overview shows no gate '{gate}'.");
            var check = box.QuerySelectorAll("li.gate-check").FirstOrDefault(c => c.QuerySelector(".gate-check-text")?.TextContent.Trim() == text)
                ?? throw new Xunit.Sdk.XunitException(
                    $"The gate '{gate}' has no check '{text}'; it has: {string.Join(" | ", box.QuerySelectorAll(".gate-check-text").Select(t => t.TextContent.Trim()))}");
            Assert.Equal(state, check.GetAttribute("data-state"));
        }

        [Then("the Overview of {string} shows no gate")]
        public async Task ThenTheOverviewShowsNoGate(string project)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.Null(_web.Page!.QuerySelector("#phase-gate"));
        }

        private async Task MoveAsync(string project, string phase, string? reason, string? supportEndsOn = null)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}");
            var fields = new Dictionary<string, string> { ["projectId"] = id.ToString(), ["phase"] = phase };
            if (supportEndsOn is not null)
            {
                fields["supportEndsOn"] = supportEndsOn;
            }

            if (reason is not null)
            {
                fields["gateReason"] = reason;
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
