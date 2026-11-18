using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-203: Views/Projects/_RisksTab.cshtml (tr.risk-row td.risk-score, #risk-matrix-mode a[data-matrix], #risk-matrix-note)
    // and Views/Risks/Details.cshtml (#risk-history li.risk-history-entry .risk-history-what).
    [Binding]
    public class RiskFromToStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskFromToStepDefinitions(WebScenario web) => _web = web;

        [When("I show the matrix after the measures")]
        public async Task WhenIShowTheMatrixAfter()
        {
            var link = _web.Page!.QuerySelector("#risk-matrix-mode a[data-matrix='after']")
                ?? throw new Xunit.Sdk.XunitException("The matrix has no \"After measures\".");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the register shows the score {string} for {string}")]
        public void ThenTheRegisterShowsTheScore(string score, string title)
        {
            var row = _web.Page!.QuerySelectorAll("tr.risk-row").FirstOrDefault(r => r.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"The register does not list '{title}'.");
            Assert.Equal(score, row.QuerySelector("td.risk-score")?.TextContent.Trim());
        }

        [Then("the matrix shows no open risk at likelihood {int} and impact {int}")]
        public void ThenTheMatrixShowsNoRisk(int likelihood, int impact)
            => Assert.Equal("", _web.Page!.QuerySelector($"#risk-matrix td[data-likelihood='{likelihood}'][data-impact='{impact}']")?.TextContent.Trim(' ', ' ', '\n', '\r'));

        [Then("the matrix says {string}")]
        public void ThenTheMatrixSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#risk-matrix-note")?.TextContent ?? "(no note)", StringComparison.Ordinal);

        [Then("the history shows {string} by {string}")]
        public void ThenTheHistoryShows(string what, string user)
        {
            var entries = _web.Page!.QuerySelectorAll("#risk-history li.risk-history-entry").ToList();
            Assert.Contains(entries, e => (e.QuerySelector(".risk-history-what")?.TextContent ?? "").Contains(what, StringComparison.Ordinal)
                && e.TextContent.Contains(user, StringComparison.Ordinal));
        }
    }
}
