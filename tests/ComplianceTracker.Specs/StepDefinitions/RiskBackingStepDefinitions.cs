using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-159: what stands behind each risk. UI contract: Views/Projects/_RisksTab.cshtml (#risks-table tr.risk-row[data-title]
    // with td.risk-linked and .risk-flags .nothing-behind; the filter checkbox name="nothing").
    [Binding]
    public class RiskBackingStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskBackingStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I show only the risks of {string} with nothing behind them")]
        public async Task WhenIShowOnlyNothingBehind(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/risks");
            var form = _web.Page!.QuerySelector("#risk-filter") ?? throw new Xunit.Sdk.XunitException("The Risks tab has no filter.");
            Assert.NotNull(form.QuerySelector("input[name=nothing]"));
            await _web.OpenAsync($"{form.GetAttribute("action")}?nothing=1");
        }

        [Then("the risk {string} is linked to {string}")]
        public void ThenTheRiskSays(string title, string text)
            => Assert.Equal(text, Row(title).QuerySelector("td.risk-linked")?.TextContent.Trim());

        [Then("the risk {string} is not marked {string}")]
        public void ThenTheRiskIsNotMarked(string title, string flag)
            => Assert.DoesNotContain(flag, Row(title).QuerySelectorAll(".risk-flags .badge").Select(b => b.TextContent.Trim()));

        [Then("the register lists only {string}")]
        public void ThenTheRegisterListsOnly(string title)
            => Assert.Equal([title], _web.Page!.QuerySelectorAll("#risks-table tr.risk-row").Select(r => r.GetAttribute("data-title")));

        private IElement Row(string title)
            => _web.Page!.QuerySelectorAll("#risks-table tr.risk-row").FirstOrDefault(r => r.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The register lists no risk '{title}'.");
    }
}
