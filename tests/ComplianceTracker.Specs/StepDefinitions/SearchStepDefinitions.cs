using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-100: the top bar's form#top-search (input name=q) and Views/Search/Index.cshtml (#search-results with
    // section.search-group[data-kind] and li.search-hit holding .search-title, .search-where and a.search-open;
    // #search-nothing).
    [Binding]
    public class SearchStepDefinitions
    {
        private readonly WebScenario _web;

        public SearchStepDefinitions(WebScenario web) => _web = web;

        [When("I search for {string}")]
        public async Task WhenISearchFor(string term)
        {
            await _web.OpenAsync("/Projects");
            var form = _web.Page!.QuerySelector("form#top-search") ?? throw new Xunit.Sdk.XunitException("The top bar has no search field.");
            Assert.NotNull(form.QuerySelector("input[name=q]"));
            await _web.OpenAsync($"{form.GetAttribute("action")}?q={Uri.EscapeDataString(term)}");
        }

        private IElement Hit(string kind, string title)
        {
            var group = _web.Page!.QuerySelector($"#search-results section.search-group[data-kind='{kind}']")
                ?? throw new Xunit.Sdk.XunitException($"The results have no {kind}. The page ({(int)_web.Response!.StatusCode}) says: "
                    + string.Join(" ", (_web.Page!.Body?.TextContent ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            return group.QuerySelectorAll("li.search-hit").SingleOrDefault(h => h.QuerySelector(".search-title")!.TextContent.Trim().StartsWith(title, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The results have no {kind} '{title}'.");
        }

        [Then("the results show the risk {string} in {string}")]
        public void ThenTheResultsShowTheRisk(string title, string project)
            => Assert.Equal(project, Hit("Risk", title).QuerySelector(".search-where")?.TextContent.Trim());

        [Then("the results show the evidence {string} in {string}")]
        public void ThenTheResultsShowTheEvidence(string title, string project)
            => Assert.Equal(project, Hit("Evidence", title).QuerySelector(".search-where")?.TextContent.Trim());

        [Then("the results show the requirement {string}")]
        public void ThenTheResultsShowTheRequirement(string code)
        {
            var hit = Hit("Requirement", code);
            Assert.StartsWith("CRA", hit.QuerySelector(".search-where")!.TextContent.Trim());
            Assert.Contains("#req-", hit.QuerySelector("a.search-open")!.GetAttribute("href"));
        }

        [Then("the risk {string} opens in the side panel of the Risks tab")]
        public async Task ThenTheRiskOpensInTheSidePanel(string title)
        {
            await _web.OpenAsync(Hit("Risk", title).QuerySelector("a.search-open")!.GetAttribute("href")!);
            Assert.False(string.IsNullOrEmpty(_web.Page!.QuerySelector("#risk-panel")?.GetAttribute("data-open-risk")));
        }

        [Then("there are no results")]
        public void ThenThereAreNoResults()
        {
            Assert.True(_web.Response!.IsSuccessStatusCode, $"The search answered {(int)_web.Response.StatusCode}.");
            Assert.Null(_web.Page!.QuerySelector("#search-results"));
            Assert.NotNull(_web.Page!.QuerySelector("#search-nothing"));
        }
    }
}
