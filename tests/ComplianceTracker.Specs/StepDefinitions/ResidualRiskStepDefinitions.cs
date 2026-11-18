using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-165: the residual risk. UI contract: Views/Risks/Details.cshtml (form#set-residual with likelihood and impact,
    // #risk-residual, form.remove-measure via li.risk-measure) and the register's .risk-level with .risk-residual.
    [Binding]
    public class ResidualRiskStepDefinitions
    {
        private readonly WebScenario _web;

        public ResidualRiskStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I set the residual risk of {string} to {int} × {int}")]
        [When("I set the residual risk of {string} to {int} × {int}")]
        public async Task WhenISetTheResidual(string title, int likelihood, int impact)
        {
            await OpenRiskAsync(title);
            var form = _web.Page!.QuerySelector("form#set-residual") ?? throw new Xunit.Sdk.XunitException("The risk page has no \"Set residual risk\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["id"] = form.QuerySelector("input[name=id]")!.GetAttribute("value")!,
                ["likelihood"] = likelihood.ToString(),
                ["impact"] = impact.ToString()
            });
        }

        [When("I remove the measure {string} from {string}")]
        public async Task WhenIRemoveTheMeasure(string measure, string title)
        {
            await OpenRiskAsync(title);
            var item = _web.Page!.QuerySelectorAll("li.risk-measure").FirstOrDefault(li => li.TextContent.Contains(measure, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no measure '{measure}'.");
            var form = item.QuerySelector("form") ?? throw new Xunit.Sdk.XunitException("The measure cannot be removed.");
            await _web.PostFormAsync(form.GetAttribute("action")!, form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? ""));
        }

        [Then("the risk {string} shows {string}")]
        public void ThenTheRiskShows(string title, string text)
        {
            var row = _web.Page!.QuerySelectorAll("tr.risk-row").FirstOrDefault(r => r.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"The register does not list '{title}'.");
            var shown = $"{row.QuerySelector(".risk-level")?.TextContent.Trim()} {Squeeze(row.QuerySelector(".risk-residual")?.TextContent)}";
            Assert.Equal(text, shown);
        }

        [Then("the risk page says {string}")]
        public void ThenTheRiskPageSays(string message)
            => Assert.Contains(message, _web.Page!.QuerySelector("[role=alert]")?.TextContent ?? "(no message)", StringComparison.Ordinal);

        private static string Squeeze(string? text) => string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private async Task OpenRiskAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }
    }
}
