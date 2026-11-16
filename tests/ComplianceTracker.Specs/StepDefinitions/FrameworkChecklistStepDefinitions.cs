using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-193: the Checklists page's #checklist-suggestions: form.suggest-checklist-items[data-framework],
    // .checklist-suggestion[data-code] with form.approve-checklist-suggestion and form.reject-checklist-suggestion
    [Binding]
    public class FrameworkChecklistStepDefinitions
    {
        private const string Path = "/Checklists";
        private readonly WebScenario _web;

        public FrameworkChecklistStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have asked the AI for checklist items for {string}")]
        [When("I ask the AI for checklist items for {string}")]
        public async Task WhenIAsk(string framework)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector($"form.suggest-checklist-items[data-framework='{framework}']")
                ?? throw new Xunit.Sdk.XunitException($"No way to ask for checklist items for {framework}.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Given("I have approved the checklist suggestion {string} as {string}")]
        public async Task GivenIHaveApproved(string code, string text)
        {
            var form = await FormAsync(code, "form.approve-checklist-suggestion");
            var fields = Hidden(form);
            fields["text"] = text;
            fields["phase"] = form.QuerySelector("select[name='phase'] option[selected]")?.GetAttribute("value") ?? "";
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Given("I have rejected the checklist suggestion {string}")]
        public async Task GivenIHaveRejected(string code)
        {
            var form = await FormAsync(code, "form.reject-checklist-suggestion");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Then("the AI suggests the checklist item {string} for the phase {string}")]
        public async Task ThenTheAiSuggests(string code, string phase)
        {
            var form = await FormAsync(code, "form.approve-checklist-suggestion");
            Assert.Equal(phase, form.QuerySelector("select[name='phase'] option[selected]")?.GetAttribute("value"));
        }

        [Then("the AI does not suggest a checklist item for {string}")]
        public async Task ThenTheAiDoesNotSuggest(string code)
        {
            await _web.OpenAsync(Path);
            Assert.Null(_web.Page!.QuerySelector($".checklist-suggestion[data-code='{code}']"));
        }

        [Then("the organisation's checklists do not list {string}")]
        public async Task ThenTheChecklistsDoNotList(string text)
        {
            await _web.OpenAsync(Path);
            Assert.Null(_web.Page!.QuerySelector($".own-item[data-text='{text}']"));
        }

        private async Task<AngleSharp.Dom.IElement> FormAsync(string code, string selector)
        {
            await _web.OpenAsync(Path);
            return _web.Page!.QuerySelector($".checklist-suggestion[data-code='{code}'] {selector}")
                ?? throw new Xunit.Sdk.XunitException($"No suggestion '{code}': {_web.Page.QuerySelector("#checklist-suggestions")?.TextContent}");
        }

        private static Dictionary<string, string> Hidden(AngleSharp.Dom.IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
