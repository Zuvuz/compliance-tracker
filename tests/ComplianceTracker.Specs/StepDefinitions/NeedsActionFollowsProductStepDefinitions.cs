using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-156: a new assessment after a change. The background job is switched off in the tests
    // (NeedsActionReassessment:Enabled = false); "the background work has run" moves the clock past the delay and runs
    // what is due, the way the job does. UI contract: _ComplianceTab.cshtml #reassessment-status[data-state],
    // _RequirementPage.cshtml .earlier-description.
    [Binding]
    public class NeedsActionFollowsProductStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly EditProjectsStepDefinitions _edit;

        public NeedsActionFollowsProductStepDefinitions(WebScenario web, ScenarioClock clock, ComplianceTabStepDefinitions compliance, EditProjectsStepDefinitions edit)
        {
            _web = web;
            _clock = clock;
            _compliance = compliance;
            _edit = edit;
        }

        [When("one minute later I change the scope of {string} to {string}")]
        public async Task WhenOneMinuteLater(string project, string scope)
        {
            _clock.SetUtcNow(_clock.GetUtcNow().AddMinutes(1));
            await _edit.WhenIChangeTheScope(project, scope);
        }

        [Given("the background work has run")]
        [When("the background work has run")]
        public async Task WhenTheBackgroundWorkHasRun()
        {
            _clock.SetUtcNow(_clock.GetUtcNow() + Domain.Mappings.NeedsActionReassessment.Delay + TimeSpan.FromMinutes(1));
            using var scope = _web.App.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<NeedsActionReassessmentJob>().RunDueAsync();
        }

        [Then("the Compliance tab of {string} says it is assessing again")]
        public Task ThenAssessingAgain(string project) => ThenStateIs(project, "pending");

        [Then("the Compliance tab of {string} says it was assessed again")]
        public Task ThenAssessedAgain(string project) => ThenStateIs(project, "done");

        [Then("the Compliance tab of {string} says the assessment is waiting for the AI")]
        public Task ThenWaitingForAi(string project) => ThenStateIs(project, "waiting-for-ai");

        [Then("the requirement page of {string} says {string}")]
        public void ThenThePageOfSays(string code, string text)
        {
            var page = RequirementRowLookup.Find(_web, code) ?? throw new Xunit.Sdk.XunitException($"No requirement '{code}'.");
            Assert.Contains(text, page.TextContent, StringComparison.Ordinal);
        }

        [Then("the AI assessed {string} again once, because {string}")]
        public async Task ThenAssessedOnce(string project, string reasons)
        {
            await using var db = _web.CreateDbContext();
            var details = await db.ActivityLog.Where(e => e.Action == ActivityAction.NeedsActionReassessed).Select(e => e.Details).ToListAsync();
            Assert.StartsWith(reasons + ":", Assert.Single(details));
        }

        [Then("the activity log has no {string}")]
        public async Task ThenTheActivityLogHasNo(string action)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<ActivityAction>(action);
            Assert.False(await db.ActivityLog.AnyAsync(e => e.Action == expected), $"The activity log has '{action}'.");
        }

        private async Task ThenStateIs(string project, string state)
        {
            await _compliance.WhenIOpenTheSubTab("CRA", project);
            var status = _web.Page!.QuerySelector("#reassessment-status") ?? throw new Xunit.Sdk.XunitException("The Compliance tab says nothing about a new assessment.");
            Assert.Equal(state, status.GetAttribute("data-state"));
        }
    }
}
