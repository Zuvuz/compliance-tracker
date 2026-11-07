using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-124: the risk's trace and the risks on a requirement row.
    // UI contract: Views/Risks/Details.cshtml (#risk-trace li.trace-node with data-kind and data-name, nested; #no-trace) and
    // Views/Projects/_ComplianceTab.cshtml (.requirement-risks with a.requirement-risk).
    [Binding]
    public class RiskTraceStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskTraceStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the trace of the risk {string}")]
        public Task WhenIOpenTheTrace(string risk) => OpenAsync(risk);

        [Then("the trace goes from the {word} {string} to the {word} {string}")]
        public void ThenTheTraceGoesFrom(string fromKind, string from, string toKind, string to)
        {
            var parents = Nodes().Where(n => Kind(n) == fromKind && n.GetAttribute("data-name") == from).ToList();
            Assert.True(parents.Count > 0, $"The trace has no {fromKind} '{from}'.");
            Assert.Contains(parents, p => p.QuerySelectorAll("li.trace-node").Any(c => Kind(c) == toKind && c.GetAttribute("data-name") == to));
        }

        [Then("the trace says {string}")]
        public void ThenTheTraceSays(string text)
            => Assert.Equal(text, _web.Page!.QuerySelector("#no-trace")?.TextContent.Trim());

        [Then("the row {string} names the risk {string}")]
        public void ThenTheRowNamesTheRisk(string code, string risk)
            => Assert.Contains(risk, Row(code).QuerySelectorAll("a.requirement-risk").Select(a => a.TextContent.Trim()));

        [Then("the row {string} names no risk")]
        public void ThenTheRowNamesNoRisk(string code)
            => Assert.Null(Row(code).QuerySelector(".requirement-risks"));

        // ---------------------------------------------------------- Helpers

        // "measure-requirement" counts as a requirement and "measure-story" as a story
        private static string Kind(IElement node) => node.GetAttribute("data-kind")!.Replace("measure-", "");

        private IEnumerable<IElement> Nodes() => _web.Page!.QuerySelectorAll("#risk-trace li.trace-node");

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no requirement '{code}'.");

        private async Task OpenAsync(string risk)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }
    }
}
