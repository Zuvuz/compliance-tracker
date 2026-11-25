using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-161: on the risk page, .measure-suggestion[data-measure] with form.approve-measure holding
    // fieldset.suggested-measure-evidence (.suggested-measure-evidence-item[data-title] with input[name=existingEvidenceId])
    // or .measure-evidence-unavailable
    [Binding]
    public class MeasureEvidenceSuggestionsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public MeasureEvidenceSuggestionsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [When("the local AI stops answering")]
        public void WhenTheAiStops() => _ai.Unavailable = true;

        [Then("the suggested measure {string} suggests the evidence {string} marked {string} with the score {string}")]
        public void ThenItSuggests(string code, string evidence, string mark, string score)
        {
            var item = Item(code, evidence) ?? throw new Xunit.Sdk.XunitException($"'{code}' does not suggest '{evidence}'.");
            Assert.Contains(mark, item.TextContent, StringComparison.Ordinal);
            Assert.Contains($"score {score}", item.TextContent, StringComparison.Ordinal);
        }

        [Then("the suggested measure {string} does not suggest the evidence {string}")]
        public void ThenItDoesNotSuggest(string code, string evidence) => Assert.Null(Item(code, evidence));

        [Then("the suggested measure {string} says {string}")]
        public void ThenItSays(string code, string text)
            => Assert.Equal(text, Form(code).QuerySelector(".measure-evidence-unavailable")?.TextContent.Trim());

        [When("I approve the suggested measure {string} with the suggested evidence {string}")]
        public async Task WhenIApproveWithTheSuggested(string code, string evidence)
        {
            var form = Form(code);
            var choice = Item(code, evidence)?.QuerySelector("input[name=existingEvidenceId]") ?? throw new Xunit.Sdk.XunitException($"'{evidence}' cannot be picked.");
            using var content = new MultipartFormDataContent();
            foreach (var input in form.QuerySelectorAll("input[type=hidden]"))
            {
                content.Add(new StringContent(input.GetAttribute("value") ?? ""), input.GetAttribute("name")!);
            }

            content.Add(new StringContent(choice.GetAttribute("value")!), "existingEvidenceId");
            content.Add(new StringContent("true"), "argumentConfirmed"); // US-221: the tick in the form
            _web.Response = await _web.Client.PostAsync(form.GetAttribute("action")!, content);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            Assert.True(_web.Response.IsSuccessStatusCode, $"Approving answered {(int)_web.Response.StatusCode}");
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        private AngleSharp.Dom.IElement Form(string code)
            => _web.Page!.QuerySelectorAll(".measure-suggestion").FirstOrDefault(m => m.GetAttribute("data-measure")!.StartsWith(code + " ", StringComparison.Ordinal))
                   ?.QuerySelector("form.approve-measure")
               ?? throw new Xunit.Sdk.XunitException($"'{code}' is not suggested.");

        private AngleSharp.Dom.IElement? Item(string code, string evidence)
            => Form(code).QuerySelectorAll(".suggested-measure-evidence-item").FirstOrDefault(i => i.GetAttribute("data-title") == evidence);
    }
}
