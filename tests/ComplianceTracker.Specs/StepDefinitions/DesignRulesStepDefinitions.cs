using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-168: the shared page header (header.page-header with h1 and .page-header-actions) and the status badges
    // (.ct-status, .ct-status-<slug>, with the fixed colour class).
    [Binding]
    public class DesignRulesStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly List<(string Path, IDocument Page)> _pages = new();

        public DesignRulesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open each main page of {string}")]
        public async Task WhenIOpenEachMainPage(string project)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            var riskId = await db.Risks.Where(r => r.ProjectId == projectId).Select(r => r.Id).FirstAsync();
            foreach (var path in new[] { $"/Projects/Details/{projectId}", $"/Projects/Details/{projectId}/risks", $"/Risks/Details/{riskId}", "/Documents" })
            {
                await _web.OpenAsync(path);
                _pages.Add((path, _web.Page!));
            }
        }

        [Then("each page has the shared header with at most one primary action")]
        public void ThenEachPageHasTheSharedHeader()
        {
            Assert.All(_pages, page =>
            {
                var header = page.Page.QuerySelector("header.page-header") ?? throw new Xunit.Sdk.XunitException($"{page.Path} has no shared page header.");
                Assert.NotNull(header.QuerySelector("h1"));
                Assert.True(header.QuerySelectorAll(".btn-primary").Length <= 1, $"{page.Path} has more than one primary action in its header.");
            });
        }

        [Then("the overview row {string} has the status {string} in red")]
        public async Task ThenTheOverviewRowHasTheStatus(string code, string status)
        {
            var row = RequirementRowLookup.ListRow(_web, code) ?? throw new Xunit.Sdk.XunitException($"No row '{code}'.");
            AssertRed(row.QuerySelector(".requirement-status"), status);
            await Task.CompletedTask;
        }

        [Then("the requirement's page has the status {string} in red")]
        public void ThenThePageHasTheStatus(string status)
            => AssertRed(_web.Page!.QuerySelector("article.requirement-page .requirement-status"), status);

        private static void AssertRed(IElement? badge, string status)
        {
            Assert.NotNull(badge);
            Assert.Equal(status, badge.TextContent.Trim());
            Assert.Contains("text-bg-danger", badge.ClassList);
            Assert.Contains("ct-status-not-covered", badge.ClassList);
        }
    }
}
