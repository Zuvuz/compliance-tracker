using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-164: Views/Projects/_FrameworksTab.cshtml (form#add-framework with optgroup#switched-on-frameworks and
    // optgroup#not-switched-on-frameworks, option[data-switches-on]).
    [Binding]
    public class FrameworkFromProjectStepDefinitions
    {
        private readonly WebScenario _web;

        public FrameworkFromProjectStepDefinitions(WebScenario web) => _web = web;

        private async Task<Guid> ProjectIdAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == name).Select(p => p.Id).SingleAsync();
        }

        [When("I open the Frameworks tab of {string}")]
        public async Task WhenIOpenTheFrameworksTab(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/frameworks");

        [When("I add {string} to {string} from its Frameworks tab")]
        public async Task WhenIAddFromTheTab(string framework, string project)
        {
            await WhenIOpenTheFrameworksTab(project);
            var form = _web.Page!.QuerySelector("form#add-framework") ?? throw new Xunit.Sdk.XunitException("The tab has no add form.");
            var option = form.QuerySelectorAll("#not-switched-on-frameworks option").SingleOrDefault(o => o.TextContent.Trim() == framework)
                ?? throw new Xunit.Sdk.XunitException($"'{framework}' is not offered under \"Not switched on yet\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["projectId"] = form.QuerySelector("input[name=projectId]")!.GetAttribute("value")!,
                ["frameworkId"] = option.GetAttribute("value")!
            });
        }

        [Then("{string} is switched on for the organisation")]
        public async Task ThenSwitchedOn(string framework)
        {
            await using var db = _web.CreateDbContext();
            var versions = await db.Frameworks.Where(f => f.Name == framework).SelectMany(f => f.Versions.Select(v => v.Id)).ToListAsync();
            Assert.True(await db.OrganizationFrameworks.AnyAsync(s => versions.Contains(s.FrameworkVersionId) && s.DisabledAt == null));
        }

        [Then("the Frameworks tab of {string} lists {string}")]
        public async Task ThenTheTabLists(string project, string framework)
        {
            await WhenIOpenTheFrameworksTab(project);
            Assert.Contains(framework, _web.Page!.QuerySelector("#project-frameworks")?.TextContent ?? _web.Page!.Body!.TextContent);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("form#add-framework option"), o => o.TextContent.Trim() == framework);
        }

        [Then("I am not offered {string}")]
        public void ThenNotOffered(string framework)
        {
            Assert.Null(_web.Page!.QuerySelector("#not-switched-on-frameworks"));
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("option"), o => o.TextContent.Trim() == framework);
        }

        // R3: the request sent anyway switches nothing on
        [Then("adding {string} anyway is refused")]
        public async Task ThenRefused(string framework)
        {
            Guid frameworkId;
            await using (var db = _web.CreateDbContext())
            {
                frameworkId = await db.Frameworks.Where(f => f.Name == framework).Select(f => f.Id).SingleAsync();
            }

            await _web.PostFormAsync("/Projects/AddFramework", new Dictionary<string, string>
            {
                ["projectId"] = (await ProjectIdAsync("Acme Portal")).ToString(),
                ["frameworkId"] = frameworkId.ToString()
            });
            await using var check = _web.CreateDbContext();
            Assert.False(await check.OrganizationFrameworks.AnyAsync());
        }
    }
}
