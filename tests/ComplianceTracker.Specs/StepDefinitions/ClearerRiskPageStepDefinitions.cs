using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-147: #risk-matrix td[data-likelihood][data-impact] a.risk-cell, #risk-status-buttons a[data-status-button],
    // #risks-table tr.risk-row[data-title] with a.risk-link[data-risk-panel], #risk-panel[data-open-risk]
    [Binding]
    public class ClearerRiskPageStepDefinitions
    {
        private readonly WebScenario _web;
        private Guid _panelRisk;

        public ClearerRiskPageStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I click the matrix cell with likelihood {int} and impact {int}")]
        public async Task WhenIClickTheCell(int likelihood, int impact)
        {
            var link = _web.Page!.QuerySelector($"#risk-matrix td[data-likelihood='{likelihood}'][data-impact='{impact}'] a.risk-cell")
                ?? throw new Xunit.Sdk.XunitException("No such cell.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I click the filter button {string}")]
        public async Task WhenIClickTheFilterButton(string text)
        {
            var link = _web.Page!.QuerySelector($"#risk-status-buttons a[data-status-button='{text}']") ?? throw new Xunit.Sdk.XunitException($"No button '{text}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I open the {string} tab of {string} with the risk {string} in the side panel")]
        public async Task WhenIOpenWithTheRiskInThePanel(string tab, string project, string risk)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            _panelRisk = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}/risks?risk={_panelRisk}");
        }

        [Then("the register lists {string}, {string} and {string} in that order")]
        public void ThenTheRegisterListsInOrder(string first, string second, string third)
            => Assert.Equal([first, second, third], Titles());

        [Then("the register lists {int} risks")]
        public void ThenTheRegisterListsCount(int count) => Assert.Equal(count, Titles().Count);

        [Then("the register lists no risks")]
        public void ThenTheRegisterListsNone() => Assert.Empty(Titles());

        [Then("the side panel is set to open {string}")]
        public void ThenThePanelOpens(string title)
        {
            Assert.Equal(_panelRisk.ToString(), _web.Page!.QuerySelector("#risk-panel")?.GetAttribute("data-open-risk"));
            var link = _web.Page.QuerySelectorAll("a.risk-link").First(a => a.TextContent.Trim() == title);
            Assert.Equal(_panelRisk.ToString(), link.GetAttribute("data-risk-panel"));
        }

        [Then("the risk's link still leads to its own page")]
        public void ThenTheLinkLeadsToThePage()
            => Assert.Contains(_web.Page!.QuerySelectorAll("a.risk-link"), a => a.GetAttribute("href") == $"/Risks/Details/{_panelRisk}");

        private List<string> Titles()
            => _web.Page!.QuerySelectorAll("#risks-table tr.risk-row").Select(r => r.GetAttribute("data-title")!).ToList();
    }
}
