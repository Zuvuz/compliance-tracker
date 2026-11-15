using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-192: the Checklists page (/Checklists): form#add-checklist-item, .own-item[data-text] with form.remove-checklist-item
    [Binding]
    public class OwnChecklistStepDefinitions
    {
        private const string Path = "/Checklists";
        private readonly WebScenario _web;

        public OwnChecklistStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have added the checklist item {string} to the phase {string} with the reference {string}")]
        public Task GivenIHaveAddedAnOwnItem(string text, string phase, string reference) => AddAsync(text, phase, null, reference);

        [Given("I have added the checklist item {string} to the phase {string} for {string}")]
        public Task GivenIHaveAddedAFrameworkItem(string text, string phase, string framework) => AddAsync(text, phase, framework, "");

        [Given("I have removed the checklist item {string}")]
        public async Task GivenIHaveRemoved(string text)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector($".own-item[data-text='{text}'] form.remove-checklist-item")
                ?? throw new Xunit.Sdk.XunitException($"'{text}' cannot be removed.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["itemId"] = form.QuerySelector("input[name='itemId']")!.GetAttribute("value")!
            });
        }

        [When("I open the organisation's checklists")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("there is no form to add a checklist item")]
        public void ThenNoForm() => Assert.Null(_web.Page!.QuerySelector("form#add-checklist-item"));

        private async Task AddAsync(string text, string phase, string? framework, string reference)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector("form#add-checklist-item") ?? throw new Xunit.Sdk.XunitException("No form to add a checklist item.");
            var source = framework is null ? ""
                : form.QuerySelectorAll("select[name='frameworkId'] option").FirstOrDefault(o => o.TextContent.Trim() == framework)?.GetAttribute("value")
                  ?? throw new Xunit.Sdk.XunitException($"The organisation does not follow {framework}.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["phase"] = phase,
                ["text"] = text,
                ["frameworkId"] = source,
                ["reference"] = reference
            });
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors"));
        }
    }
}
