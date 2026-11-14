using System.Text;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-158: approving a suggested measure with evidence. UI contract: Views/Risks/Details.cshtml (.measure-suggestion
    // [data-measure] with form.approve-measure: evidenceTitle, evidenceUrl, evidenceFile, evidenceValidUntil;
    // #risk-measures li.risk-measure with a.measure-add-evidence).
    [Binding]
    public class MeasureEvidenceStepDefinitions
    {
        private readonly WebScenario _web;

        public MeasureEvidenceStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I approve the suggested measure {string} with the evidence {string} and the link {string}")]
        public Task WhenIApproveWithALink(string code, string title, string url)
            => PostAsync(code, new Dictionary<string, string> { ["evidenceTitle"] = title, ["evidenceUrl"] = url }, null);

        [When("I approve the suggested measure {string} with the evidence file {string}")]
        public Task WhenIApproveWithAFile(string code, string fileName)
            => PostAsync(code, [], (fileName, Encoding.ASCII.GetBytes("%PDF-1.7\n% hardening\n")));

        [When("I approve the suggested measure {string} with both a link and a file")]
        public Task WhenIApproveWithBoth(string code)
            => PostAsync(code, new Dictionary<string, string> { ["evidenceUrl"] = "https://example.com/x" }, ("x.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n")));

        [Then("the risk {string} has no measure {string}")]
        public async Task ThenTheRiskHasNoMeasure(string risk, string code)
        {
            await OpenRiskAsync(risk);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#risk-measures li.risk-measure"), m => m.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal));
        }

        [Then("the measure {string} of the risk {string} does not offer {string}")]
        public async Task ThenTheMeasureDoesNotOffer(string code, string risk, string text)
        {
            await OpenRiskAsync(risk);
            Assert.Null(Measure(code).QuerySelector("a.measure-add-evidence"));
        }

        [Then("the measure {string} of the risk {string} offers {string} on the page of {string}")]
        public async Task ThenTheMeasureOffers(string code, string risk, string text, string page)
        {
            await OpenRiskAsync(risk);
            var link = Measure(code).QuerySelector("a.measure-add-evidence") ?? throw new Xunit.Sdk.XunitException($"'{code}' offers no \"{text}\".");
            Assert.Equal(text, link.TextContent.Trim());
            Assert.EndsWith("/compliance/cra/" + page, Uri.UnescapeDataString(link.GetAttribute("href")!));
        }

        // The approve form of the suggestion, with the evidence fields; a file goes as multipart
        private async Task PostAsync(string code, Dictionary<string, string> fields, (string Name, byte[] Content)? file)
        {
            var form = _web.Page!.QuerySelectorAll(".measure-suggestion").FirstOrDefault(m => m.GetAttribute("data-measure")!.StartsWith(code + " ", StringComparison.Ordinal))
                ?.QuerySelector("form.approve-measure") ?? throw new Xunit.Sdk.XunitException($"'{code}' is not suggested.");
            Assert.NotNull(form.QuerySelector("input[name=evidenceUrl]"));

            using var content = new MultipartFormDataContent();
            foreach (var input in form.QuerySelectorAll("input[type=hidden]"))
            {
                content.Add(new StringContent(input.GetAttribute("value") ?? ""), input.GetAttribute("name")!);
            }

            // The form's hidden fields hold the antiforgery token already
            foreach (var (name, value) in fields)
            {
                content.Add(new StringContent(value), name);
            }

            if (file is { } f)
            {
                var bytes = new ByteArrayContent(f.Content);
                bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(bytes, "evidenceFile", f.Name);
            }

            _web.Response = await _web.Client.PostAsync(form.GetAttribute("action")!, content);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            if (!_web.Response.IsSuccessStatusCode)
            {
                throw new Xunit.Sdk.XunitException($"Approving answered {(int)_web.Response.StatusCode}: {(await _web.Response.Content.ReadAsStringAsync())[..Math.Min(300, (int)(_web.Response.Content.Headers.ContentLength ?? 300))]}");
            }
        }

        private IElement Measure(string code)
            => _web.Page!.QuerySelectorAll("#risk-measures li.risk-measure").FirstOrDefault(m => m.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal))
               ?? throw new Xunit.Sdk.XunitException($"The risk has no measure '{code}'.");

        private async Task OpenRiskAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }
    }
}
