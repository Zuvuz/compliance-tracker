using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-173: <term> (span.term with a.term-help[data-term][data-explanation]) and the Help page (#glossary dt[id])
    [Binding]
    public class GlossaryStepDefinitions
    {
        private readonly WebScenario _web;
        private string? _anchor;

        public GlossaryStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("{string} has a {string} that says {string} and names {string}")]
        public void ThenTheTermHasHelp(string term, string mark, string explanation, string source)
        {
            var help = Help(term);
            Assert.Equal(mark, help.TextContent.Trim());
            Assert.Contains(explanation, help.GetAttribute("data-explanation"), StringComparison.Ordinal);
            Assert.Contains(source, help.GetAttribute("data-explanation"), StringComparison.Ordinal);
        }

        [When("I follow the {string} of {string}")]
        public async Task WhenIFollow(string mark, string term)
        {
            var href = Help(term).GetAttribute("href")!;
            _anchor = href.Split('#')[1];
            await _web.OpenAsync(href.Split('#')[0]);
        }

        [Then("the Help page explains {string} with its source")]
        public void ThenTheHelpPageExplains(string term)
        {
            var entry = _web.Page!.QuerySelector($"#glossary dt#{_anchor}") ?? throw new Xunit.Sdk.XunitException($"The glossary has no #{_anchor}.");
            Assert.Equal(term, entry.TextContent.Trim());
            Assert.False(string.IsNullOrWhiteSpace(entry.NextElementSibling?.QuerySelector(".glossary-source")?.TextContent));
        }

        private IElement Help(string term)
            => _web.Page!.QuerySelectorAll("a.term-help").FirstOrDefault(a => a.GetAttribute("data-term") == term)
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no \"?\" for '{term}'.");
    }
}
