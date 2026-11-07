using ComplianceTracker.Specs.Support;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-056: the technical documentation report on the Reports tab (US-129, a#reports-technical-documentation) and the DOCX it gives
    [Binding]
    public class TechnicalDocumentationStepDefinitions
    {
        private readonly WebScenario _web;
        private List<(string Style, string Text)> _paragraphs = new();

        public TechnicalDocumentationStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I make the technical documentation draft of {string}")]
        public async Task WhenIMakeTheDraft(string project)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/reports");
            var href = _web.Page!.QuerySelector("a#reports-technical-documentation")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException($"'{project}' has no technical documentation report.");
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            using var word = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
            _paragraphs = word.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
                .Select(p => (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "", p.InnerText))
                .ToList();
        }

        [When("I ask for the technical documentation draft of {string} directly")]
        public async Task WhenIAskDirectly(string project)
            => _web.Response = await _web.Client.GetAsync($"/Projects/TechnicalDocumentation/{await ProjectIdAsync(project)}");

        // R1: the eight points of Annex VII, in order
        [Then("the draft has the headings of CRA Annex VII")]
        public void ThenTheDraftHasTheHeadings()
        {
            var headings = _paragraphs.Where(p => p.Style == "Heading1").Select(p => p.Text).ToList();
            Assert.Equal(Enumerable.Range(1, 8).Select(n => $"{n}."), headings.Select(h => h.Split(' ')[0]));
            Assert.Contains("8. Software bill of materials", headings);
        }

        [Then("the draft says it was made by {string}")]
        public void ThenTheDraftSaysItWasMadeBy(string user)
            => Assert.Contains(_paragraphs, p => p.Text.Contains($"by {user},"));

        [Then("the draft says {string} is met by {string}")]
        public void ThenTheDraftSaysIsMetBy(string code, string story)
            => Assert.Contains(_paragraphs, p => p.Text.Contains(code + " ") && p.Text.Contains("met by " + story));

        [Then("the draft says {string} does not apply because {string}")]
        public void ThenTheDraftSaysDoesNotApply(string code, string reason)
            => Assert.Contains(_paragraphs, p => p.Text.Contains(code + " ") && p.Text.Contains("does not apply: " + reason));

        [Then("the section {string} says {string}")]
        public void ThenTheSectionSays(string section, string text)
        {
            var start = _paragraphs.FindIndex(p => p.Style == "Heading1" && p.Text.Contains(section));
            Assert.True(start >= 0, $"The draft has no section '{section}'.");
            var body = _paragraphs.Skip(start + 1).TakeWhile(p => p.Style != "Heading1").Select(p => p.Text);
            Assert.Contains(body, t => t.Contains(text));
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
