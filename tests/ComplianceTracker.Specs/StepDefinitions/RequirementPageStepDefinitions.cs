using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-153: the light overview and the requirement's own page. UI contract: Views/Projects/_RequirementSummaryRow.cshtml
    // (tr.requirement-row with a.requirement-open, .row-summary, .row-waiting, .requirement-status, .failing-tests) and
    // Views/Projects/_RequirementPage.cshtml (article.requirement-row[data-detail], #primary-action, #requirement-more).
    [Binding]
    public class RequirementPageStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;

        public RequirementPageStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        [When("I open the requirement {string} from the overview")]
        public async Task WhenIOpenTheRequirement(string code)
        {
            await _compliance.WhenIOpenTheSubTab("CRA", "Acme Portal");
            var link = RequirementRowLookup.ListRow(_web, code)?.QuerySelector("a.requirement-open")
                ?? throw new Xunit.Sdk.XunitException($"The overview has no link to '{code}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the overview row {string} says {string} and {string}")]
        public async Task ThenTheOverviewRowSays(string code, string summary, string status)
        {
            var row = await ListRowAsync(code);
            Assert.Equal(summary, Normalize(row.QuerySelector(".row-summary")!.TextContent));
            Assert.Equal(status, row.QuerySelector(".requirement-status")!.TextContent.Trim());
        }

        [Then("the overview row {string} shows {string}")]
        public async Task ThenTheOverviewRowShows(string code, string text)
            => Assert.Contains(text, Normalize((await ListRowAsync(code)).TextContent));

        [Then("the overview has no forms in its rows")]
        public void ThenTheOverviewHasNoForms()
        {
            var rows = _web.Page!.QuerySelectorAll("#requirements-table tr.requirement-row").ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Empty(r.QuerySelectorAll("form, select, input, textarea")));
        }

        [Then("I am on the page of {string}")]
        public void ThenIAmOnThePageOf(string code)
        {
            Assert.EndsWith("/compliance/cra/" + code, Uri.UnescapeDataString(_web.CurrentPath));
            Assert.NotNull(_web.Page!.QuerySelectorAll("article.requirement-row[data-detail='true']").SingleOrDefault(a => a.GetAttribute("data-code") == code));
        }

        [Then("the page has the primary action {string} and the actions {string} and {string} under {string}")]
        public void ThenThePageHasThePrimaryAction(string primary, string first, string second, string more)
        {
            // R5: one primary action on the page
            var primaries = _web.Page!.QuerySelectorAll("article.requirement-page .btn-primary").Where(b => b.Id == "primary-action").ToList();
            Assert.Equal(primary, Assert.Single(primaries).TextContent.Trim());
            var menu = _web.Page!.QuerySelector("#requirement-more") ?? throw new Xunit.Sdk.XunitException("No 'More' menu.");
            Assert.Equal(more, menu.QuerySelector("summary")!.TextContent.Trim());
            var labels = menu.QuerySelectorAll("button, summary").Select(b => b.TextContent.Trim()).ToList();
            Assert.Contains(first, labels);
            Assert.Contains(second, labels);
        }

        [Then("the page shows an error")]
        public void ThenThePageShowsAnError()
            => Assert.False(string.IsNullOrWhiteSpace(_web.Page!.QuerySelector("[role=alert].text-danger")?.TextContent), "No error is shown.");

        [Then("the page is under {int} KB")]
        public async Task ThenThePageIsUnder(int kilobytes)
        {
            var bytes = (await _web.Response!.Content.ReadAsByteArrayAsync()).Length;
            Assert.True(bytes < kilobytes * 1024, $"The page {_web.CurrentPath} is {bytes / 1024} KB, more than {kilobytes} KB.");
        }

        // The light row on the overview, going back to it from a requirement's page
        private async Task<IElement> ListRowAsync(string code)
        {
            if (RequirementRowLookup.ListRow(_web, code) is null && _web.Page?.QuerySelector("#back-to-requirements") is { } back)
            {
                await _web.OpenAsync(back.GetAttribute("href")!);
            }

            return RequirementRowLookup.ListRow(_web, code) ?? throw new Xunit.Sdk.XunitException($"The overview has no row '{code}'.");
        }

        private static string Normalize(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
