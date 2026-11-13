using System.Text;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-149: diagrams on the Evidence tab (Views/Projects/_EvidenceTab.cshtml: form#evidence-upload,
    // .evidence-item[data-title] with .diagram-summary, .diagram-link, .diagram-element, .diagram-error).
    [Binding]
    public class DiagramEvidenceStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;

        public DiagramEvidenceStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // A draw.io file with three boxes in a row and labelled arrows between them, as the editor saves it
        [When("I upload the diagram {string} with {string}, {string} and {string}")]
        public Task WhenIUploadTheDrawio(string fileName, string first, string second, string third)
            => UploadAsync(fileName, $"""
                <mxfile host="app.diagrams.net"><diagram id="p1" name="Page-1"><mxGraphModel><root>
                  <mxCell id="0"/><mxCell id="1" parent="0"/>
                  <mxCell id="a" value="{first}" style="rounded=1;" vertex="1" parent="1"/>
                  <mxCell id="b" value="{second}" vertex="1" parent="1"/>
                  <mxCell id="c" value="{third}" style="shape=cylinder3;" vertex="1" parent="1"/>
                  <mxCell id="e1" value="HTTPS" edge="1" source="a" target="b" parent="1"/>
                  <mxCell id="e2" value="SQL" edge="1" source="b" target="c" parent="1"/>
                </root></mxGraphModel></diagram></mxfile>
                """);

        [When("I upload the diagram {string} with the text:")]
        public Task WhenIUploadTheDiagramWithText(string fileName, string text) => UploadAsync(fileName, text);

        [Then("the evidence {string} says {string}")]
        public async Task ThenTheEvidenceSays(string title, string summary)
            => Assert.Equal(summary, (await ItemAsync(title)).QuerySelector(".diagram-summary")?.TextContent.Trim());

        [Then("the evidence {string} lists the link {string}")]
        public async Task ThenListsTheLink(string title, string link)
            => Assert.Contains(link, (await ItemAsync(title)).QuerySelectorAll(".diagram-link").Select(l => l.TextContent.Trim()));

        [Then("the evidence {string} lists the element {string}")]
        public async Task ThenListsTheElement(string title, string element)
            => Assert.Contains(element, (await ItemAsync(title)).QuerySelectorAll(".diagram-element").Select(e => e.TextContent.Trim()));

        [Then("the evidence {string} explains {string}")]
        public async Task ThenExplains(string title, string text)
            => Assert.Equal(text, (await ItemAsync(title)).QuerySelector(".diagram-error")?.TextContent.Trim());

        [Then("the Evidence tab does not show {string}")]
        public async Task ThenTheTabDoesNotShow(string text)
        {
            await OpenTabAsync();
            Assert.DoesNotContain(text, _web.Page!.Body!.TextContent, StringComparison.Ordinal);
            Assert.Empty(_web.Page!.QuerySelectorAll("#evidence-list script"));
        }

        // Uploads like a user: the Evidence tab's form with the file, its name as the title
        private async Task UploadAsync(string fileName, string content)
        {
            await OpenTabAsync();
            var form = _web.Page!.QuerySelector("#evidence-upload")
                ?? throw new Xunit.Sdk.XunitException("The Evidence tab has no upload form.");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["title"] = fileName;
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, Encoding.UTF8.GetBytes(content));
        }

        private async Task<IElement> ItemAsync(string title)
        {
            await OpenTabAsync();
            return _web.Page!.QuerySelectorAll("#evidence-list .evidence-item").FirstOrDefault(e => e.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"The Evidence tab does not list '{title}'.");
        }

        private async Task OpenTabAsync()
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/evidence");
        }
    }
}
