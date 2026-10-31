using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-022: the project page's tabs, through the web app with test sign-in.
    // UI contract: Views/Projects/_ProjectHeader.cshtml (#project-name, #project-phase, #project-intended-purpose,
    // #project-tabs with links data-tab) and the tab partials (#framework-summary, #project-frameworks rows,
    // #project-activity rows activity-row with activity-action, activity-user and activity-details).
    [Binding]
    public class ProjectTabsStepDefinitions
    {
        private readonly WebScenario _web;

        public ProjectTabsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- When

        // Like a user: opens the project page and clicks the tab
        [When("I open the {string} tab of {string}")]
        public async Task WhenIOpenTheTab(string tabTitle, string projectName)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}");
            var link = Tabs().FirstOrDefault(a => a.TextContent.Trim() == tabTitle)
                ?? throw new Xunit.Sdk.XunitException($"The project page has no tab '{tabTitle}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        // R6: an address typed or shared directly; {id} is the project's id
        [When("I open the address {string} of {string}")]
        public async Task WhenIOpenTheAddress(string address, string projectName)
            => await _web.OpenAsync(address.Replace("{id}", (await ProjectIdAsync(projectName)).ToString()));

        [When("{string} opens the address {string} of {string}")]
        public async Task WhenOpensTheAddress(string userName, string address, string projectName)
        {
            var path = address.Replace("{id}", (await ProjectIdAsync(projectName)).ToString());
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync(path);
        }

        // ---------------------------------------------------------- Then

        [Then("the header shows {string} in phase {string} with intended purpose {string}")]
        public void ThenTheHeaderShows(string name, string phase, string purpose)
        {
            Assert.Equal(name, Text("#project-name"));
            Assert.Equal(phase, Text("#project-phase"));
            Assert.Equal(purpose, Text("#project-intended-purpose"));
        }

        [Then("the {string} tab is active")]
        public void ThenTheTabIsActive(string tabTitle)
        {
            var active = Tabs().Where(a => a.ClassList.Contains("active")).Select(a => a.TextContent.Trim());
            Assert.Equal([tabTitle], active);
        }

        [Then("the summary says {string}")]
        public void ThenTheSummarySays(string summary)
            => Assert.Equal(summary, Text("#framework-summary"));

        [Then("the row for {string} shows source {string}, version {string}, status {string} and added by {string}")]
        public void ThenTheRowShows(string frameworkName, string source, string version, string status, string addedBy)
        {
            var row = FrameworkRow(frameworkName);
            Assert.Equal(source, Cell(row, "framework-source"));
            Assert.Equal(version, Cell(row, "framework-version"));
            Assert.Equal(status, Cell(row, "framework-status"));
            Assert.Equal(addedBy, Cell(row, "framework-added-by"));
        }

        [Then("the row for {string} was added on {string}")]
        public void ThenTheRowWasAddedOn(string frameworkName, string date)
            => Assert.Equal(date, Cell(FrameworkRow(frameworkName), "framework-added-on"));

        [Then("the first row is {string} by {string}")]
        public void ThenTheFirstRowIs(string action, string user)
            => AssertRow(ActivityRows().First(), action, user);

        [Then("the last row is {string} by {string}")]
        public void ThenTheLastRowIs(string action, string user)
            => AssertRow(ActivityRows().Last(), action, user);

        [Then("the first row is {string} by {string} with {string}")]
        public void ThenTheFirstRowIsWith(string action, string user, string details)
        {
            var row = ActivityRows().First();
            AssertRow(row, action, user);
            Assert.Equal(details, Cell(row, "activity-details"));
        }

        // ---------------------------------------------------------- Helpers

        private IEnumerable<IElement> Tabs() => _web.Page!.QuerySelectorAll("#project-tabs a[data-tab]");

        private string? Text(string selector) => _web.Page!.QuerySelector(selector)?.TextContent.Trim();

        private static string? Cell(IElement row, string className) => row.QuerySelector($".{className}")?.TextContent.Trim();

        private IElement FrameworkRow(string frameworkName)
            => _web.Page!.QuerySelectorAll("#project-frameworks tbody tr")
                   .FirstOrDefault(r => Cell(r, "framework-name") == frameworkName)
               ?? throw new Xunit.Sdk.XunitException($"The Frameworks tab has no row for '{frameworkName}'.");

        private IReadOnlyList<IElement> ActivityRows()
        {
            var rows = _web.Page!.QuerySelectorAll("#project-activity tr.activity-row").ToList();
            Assert.NotEmpty(rows);
            return rows;
        }

        private static void AssertRow(IElement row, string action, string user)
        {
            Assert.Equal(action, Cell(row, "activity-action"));
            Assert.Equal(user, Cell(row, "activity-user"));
        }

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
