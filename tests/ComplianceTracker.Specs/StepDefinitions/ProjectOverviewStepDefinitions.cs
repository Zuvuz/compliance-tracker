using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-048: the Overview tab, through the web app with test sign-in.
    // UI contract: Views/Projects/_OverviewTab.cshtml (#coverage-summary li[data-framework] .coverage-percent,
    // #needs-attention li, #risk-summary a[data-level] .risk-count, #latest-activity li .activity-text).
    [Binding]
    public class ProjectOverviewStepDefinitions
    {
        private readonly WebScenario _web;

        public ProjectOverviewStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // Another member, with their own sign-in; the page is kept for the Then steps
        [When("{string} opens the {string} tab of {string}")]
        public async Task WhenOpensTheTab(string userName, string tab, string projectName)
        {
            // Any tab, by the text on it (US-104 uses "Product requirements")
            var slug = ComplianceTracker.Web.Models.ProjectTabs.All.SingleOrDefault(t => t.Title == tab).Slug
                ?? throw new Xunit.Sdk.XunitException($"There is no tab '{tab}'.");

            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync($"/Projects/Details/{id}/{slug}");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        [Then("the coverage of {string} is {string}")]
        public void ThenTheCoverageIs(string framework, string percent)
        {
            var item = _web.Page!.QuerySelector($"#coverage-summary li[data-framework='{framework}']")
                ?? throw new Xunit.Sdk.XunitException($"The overview shows no coverage for '{framework}'.");
            Assert.Equal(percent, item.QuerySelector(".coverage-percent")?.TextContent.Trim());
        }

        [Then("\"Needs attention\" says {string}")]
        public void ThenNeedsAttentionSays(string text)
            => Assert.Contains(text, AttentionItems().Select(Text));

        [Then("{string} links to the {string} sub-tab showing {string}")]
        public async Task ThenLinksToTheSubTabShowing(string text, string subTab, string filter)
        {
            var link = AttentionItems().First(li => Text(li) == text).QuerySelector("a")
                ?? throw new Xunit.Sdk.XunitException($"'{text}' is not a link.");
            await _web.OpenAsync(link.GetAttribute("href")!);

            Assert.Equal([subTab], _web.Page!.QuerySelectorAll("#compliance-tabs a.active").Select(a => a.TextContent.Trim()));
            Assert.Equal(filter, _web.Page!.QuerySelector("#compliance-filters a[aria-current='true']")?.TextContent.Trim());
        }

        [Then("the risk summary shows {int} {string}")]
        public void ThenTheRiskSummaryShows(int count, string level)
        {
            var item = _web.Page!.QuerySelector($"#risk-summary a[data-level='{level}']")
                ?? throw new Xunit.Sdk.XunitException($"The risk summary has no '{level}'.");
            Assert.Equal(count.ToString(), item.QuerySelector(".risk-count")?.TextContent.Trim());
        }

        [Then("the latest activity starts with {string}")]
        public void ThenTheLatestActivityStartsWith(string text)
        {
            var first = _web.Page!.QuerySelector("#latest-activity li .activity-text")
                ?? throw new Xunit.Sdk.XunitException("The overview shows no activity.");
            Assert.StartsWith(text, Text(first));
        }

        private IEnumerable<IElement> AttentionItems() => _web.Page!.QuerySelectorAll("#needs-attention li");

        private static string Text(IElement element)
            => string.Join(" ", element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
