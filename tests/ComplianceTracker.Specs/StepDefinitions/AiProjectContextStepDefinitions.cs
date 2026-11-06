using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-060, US-061: the AI's context and its "not applicable" suggestions, through the web app with the pretend AI.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (form #suggest-not-applicable; per row .high-risk,
    // .applicability-suggestion with .applicability-reason and the forms .accept-applicability and .reject-applicability,
    // .suggestion-context, .old-classification and the form .keep-not-applicable) and Views/Risks/Details.cshtml (#add-measure).
    [Binding]
    public class AiProjectContextStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;
        private readonly ComplianceTabStepDefinitions _compliance;

        public AiProjectContextStepDefinitions(WebScenario web, FakeAi ai, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _ai = ai;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given

        [Given("the AI says {string} does not apply because {string}")]
        public void GivenTheAiSaysDoesNotApply(string code, string reason)
            => _ai.NotApplicable[code] = reason;

        // Chooses the requirement in the risk page's "Link measure" list by its code
        [Given("the risk {string} has the requirement {string} as a measure")]
        public async Task GivenTheRiskHasTheRequirement(string risk, string code)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
                await _web.OpenAsync($"/Risks/Details/{id}");
            }

            var form = _web.Page!.QuerySelector("#add-measure")!;
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be linked to the risk.");
            var fields = HiddenFields(form);
            fields["measure"] = option.GetAttribute("value")!;
            await _web.PostFormAsync("/Risks/AddMeasure", fields);
        }

        [Given("I have asked for \"not applicable\" suggestions on the {string} sub-tab of {string}")]
        [When("I ask for \"not applicable\" suggestions on the {string} sub-tab of {string}")]
        public async Task WhenIAskForNotApplicableSuggestions(string subTab, string project)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, project);
            var form = _web.Page!.QuerySelector("form#suggest-not-applicable")
                ?? throw new Xunit.Sdk.XunitException("The sub-tab has no \"Suggest not applicable\" button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [Given("I have accepted the \"not applicable\" suggestion on the row {string}")]
        public Task GivenIHaveAccepted(string code)
            => AcceptAsync(code, reason: null);

        // ---------------------------------------------------------- When

        [When("I accept the \"not applicable\" suggestion on the row {string} with the reason {string}")]
        public Task WhenIAccept(string code, string reason)
            => AcceptAsync(code, reason);

        [When("I reject the \"not applicable\" suggestion on the row {string}")]
        public async Task WhenIReject(string code)
        {
            var form = Row(code).QuerySelector("form.reject-applicability")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no Reject button for a \"not applicable\" suggestion.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I keep {string} not applicable on the {string} sub-tab of {string}")]
        public async Task WhenIKeepNotApplicable(string code, string subTab, string project)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, project);
            var form = Row(code).QuerySelector("form.keep-not-applicable")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no \"Still not applicable\" button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        // ---------------------------------------------------------- Then

        // The rationale is written in the background, so the page is opened again until it is there (at most 10 s)
        [Then("the context used for the suggestion on the row {string} becomes {string}")]
        public async Task ThenTheContextBecomes(string code, string context)
        {
            var path = _web.CurrentPath;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                await _web.OpenAsync(path);
                var text = Row(code).QuerySelector(".ai-suggestion .suggestion-context")?.TextContent.Trim();
                if (text == $"Context used: {context}")
                {
                    return;
                }

                await Task.Delay(200);
            }

            Assert.Fail($"The context used for '{code}' never became '{context}'.");
        }

        [Then("the AI was told {string}")]
        public void ThenTheAiWasTold(string text)
        {
            lock (_ai)
            {
                Assert.Contains(_ai.Prompts, p => p.Contains(text, StringComparison.Ordinal));
            }
        }

        [Then("the row {string} is marked {string}")]
        public void ThenTheRowIsMarked(string code, string mark)
            => Assert.Equal(mark, Row(code).QuerySelector(".high-risk")?.TextContent.Trim());

        [Then("the row {string} is not marked {string}")]
        public void ThenTheRowIsNotMarked(string code, string mark)
            => Assert.Null(Row(code).QuerySelector(".high-risk"));

        [Then("the row {string} suggests \"not applicable\" because {string}")]
        public void ThenTheRowSuggestsNotApplicable(string code, string reason)
            => Assert.Equal(reason, Row(code).QuerySelector(".applicability-suggestion .applicability-reason")?.TextContent.Trim());

        [Then("the row {string} has no \"not applicable\" suggestion")]
        public void ThenTheRowHasNoNotApplicableSuggestion(string code)
            => Assert.Null(Row(code).QuerySelector(".applicability-suggestion"));

        [Then("the row {string} says it is based on an earlier classification")]
        public void ThenTheRowSaysEarlierClassification(string code)
            => Assert.Equal("Based on an earlier classification", Row(code).QuerySelector(".old-classification")?.TextContent.Trim());

        [Then("the row {string} does not say it is based on an earlier classification")]
        public void ThenTheRowDoesNotSayEarlierClassification(string code)
            => Assert.Null(Row(code).QuerySelector(".old-classification"));

        // ---------------------------------------------------------- Helpers

        private async Task AcceptAsync(string code, string? reason)
        {
            var form = Row(code).QuerySelector("form.accept-applicability")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no Accept button for a \"not applicable\" suggestion.");
            var fields = HiddenFields(form);
            fields["reason"] = reason ?? form.QuerySelector("input[name=reason]")!.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
