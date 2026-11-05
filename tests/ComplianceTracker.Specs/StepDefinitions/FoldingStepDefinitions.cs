using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Issue #52: .fold-toggle[data-fold-target] opens and folds .fold-part[data-fold-key]; .fold-summary[data-fold-summary]
    // shows when folded; data-attention="true" keeps the part open (wwwroot/js/site.js)
    [Binding]
    public class FoldingStepDefinitions
    {
        private readonly WebScenario _web;

        public FoldingStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the Evidence tab of {string}")]
        public async Task WhenIOpenTheEvidenceTab(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/evidence");
        }

        [Then("the evidence card {string} can be folded, with the summary {string}")]
        public void ThenTheEvidenceCardCanBeFolded(string title, string summary)
            => AssertFoldable(Card(title), summary);

        [Then("the evidence card {string} needs attention")]
        public void ThenTheEvidenceCardNeedsAttention(string title)
            => Assert.Equal("true", Card(title).GetAttribute("data-attention"));

        [Then("the row {string} can be folded, with the summary {string}")]
        public void ThenTheRowCanBeFolded(string code, string summary)
            => AssertFoldable(Row(code), summary);

        [Then("the row {string} needs attention")]
        public void ThenTheRowNeedsAttention(string code)
            => Assert.Equal("true", Row(code).GetAttribute("data-attention"));

        [Then("the row {string} does not need attention")]
        public void ThenTheRowDoesNotNeedAttention(string code)
            => Assert.Equal("false", Row(code).GetAttribute("data-attention"));

        // The button, the part and the summary share a key; the summary is hidden until the part is folded
        private static void AssertFoldable(IElement container, string summary)
        {
            var toggle = container.QuerySelector(".fold-toggle") ?? throw new Xunit.Sdk.XunitException("No fold button.");
            var key = toggle.GetAttribute("data-fold-target")!;
            Assert.NotNull(container.QuerySelectorAll(".fold-part").SingleOrDefault(p => p.GetAttribute("data-fold-key") == key));
            var text = container.QuerySelectorAll(".fold-summary").Single(s => s.GetAttribute("data-fold-summary") == key);
            Assert.True(text.HasAttribute("hidden"));
            Assert.StartsWith(summary, string.Join(" ", text.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        }

        private IElement Card(string title)
            => _web.Page!.QuerySelectorAll(".evidence-item").FirstOrDefault(c => c.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"No evidence card '{title}'.");

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"No requirement row '{code}'.");
    }
}
