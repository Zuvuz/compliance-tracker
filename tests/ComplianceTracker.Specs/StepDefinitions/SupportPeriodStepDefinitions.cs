using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-025: the phase form #change-phase (supportEndsOn, supportReason), the card #support-period with the form
    // #set-support-period, and the header #project-support
    [Binding]
    public class SupportPeriodStepDefinitions
    {
        private readonly WebScenario _web;

        public SupportPeriodStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("{string} is released with support until {string}")]
        [When("I release {string} with support until {string}")]
        public Task WhenIRelease(string project, string date)
            => ReleaseAsync(project, date, null);

        [When("I release {string} with support until {string} and no reason")]
        public Task WhenIReleaseWithoutReason(string project, string date)
            => ReleaseAsync(project, date, null);

        [When("I release {string} with support until {string} and the reason {string}")]
        public Task WhenIReleaseWithReason(string project, string date, string reason)
            => ReleaseAsync(project, date, reason);

        [When("I set the support end date of {string} to {string}")]
        public async Task WhenISetTheSupportEndDate(string project, string date)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}");
            Assert.NotNull(_web.Page!.QuerySelector("#set-support-period"));
            await _web.PostFormAsync("/Projects/SetSupportPeriod", new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["supportEndsOn"] = date
            });
        }

        [Then("the header of {string} shows {string}")]
        public async Task ThenTheHeaderShows(string project, string text)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.Equal(text, _web.Page!.QuerySelector("#project-support")?.TextContent.Trim());
        }

        // The release form on the Overview tab, like a user
        private async Task ReleaseAsync(string project, string date, string? reason)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}");
            var form = _web.Page!.QuerySelector("#change-phase")
                ?? throw new Xunit.Sdk.XunitException("The project has no phase form.");
            Assert.NotNull(form.QuerySelector("input[name=supportEndsOn]"));

            var fields = new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["phase"] = "Released",
                ["supportEndsOn"] = date
            };
            if (reason is not null)
            {
                fields["supportReason"] = reason;
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
