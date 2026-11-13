using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-080: the industry section on /Settings (Views/Settings/Index.cshtml: form#choose-industry with industry and
    // productsUseAi, #industry-suggestions li.industry-suggestion[data-name] with .suggestion-reason, .suggestion-state
    // and form.switch-on-suggestion, #industry-coming, #industry-no-suggestions).
    [Binding]
    public class IndustryProfileStepDefinitions
    {
        private const string SettingsPath = "/Settings";

        private readonly WebScenario _web;

        public IndustryProfileStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have chosen the industry {string}")]
        [When("I choose the industry {string}")]
        public Task WhenIChoose(string industry) => ChooseAsync(industry, usesAi: false);

        [When("I choose the industry {string} with AI in the products")]
        public Task WhenIChooseWithAi(string industry) => ChooseAsync(industry, usesAi: true);

        [When("I open the projects page")]
        public Task WhenIOpenTheProjectsPage() => _web.OpenAsync("/Projects");

        [When("I switch on the suggestion {string}")]
        public async Task WhenISwitchOn(string name)
        {
            var form = (await SuggestionAsync(name)).QuerySelector("form.switch-on-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"The suggestion '{name}' has no \"Switch on\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Then("the industry suggests {string} because {string}")]
        public async Task ThenSuggests(string name, string reason)
            => Assert.Equal(reason, (await SuggestionAsync(name)).QuerySelector(".suggestion-reason")?.TextContent.Trim());

        [Then("the industry does not suggest {string}")]
        public async Task ThenDoesNotSuggest(string name)
        {
            await _web.OpenAsync(SettingsPath);
            Assert.DoesNotContain(name, Suggestions().Select(s => s.GetAttribute("data-name")));
        }

        [Then("the industry makes no suggestions")]
        public async Task ThenNoSuggestions()
        {
            await _web.OpenAsync(SettingsPath);
            Assert.Empty(Suggestions());
            Assert.NotNull(_web.Page!.QuerySelector("#industry-no-suggestions"));
        }

        [Then("the suggestion {string} is {string}")]
        public async Task ThenTheSuggestionIs(string name, string state)
            => Assert.Equal(state, (await SuggestionAsync(name)).QuerySelector(".suggestion-state")?.TextContent.Trim());

        [Then("I am told the suggestions for {string} are coming")]
        public async Task ThenComing(string industry)
        {
            await _web.OpenAsync(SettingsPath);
            Assert.StartsWith($"Suggestions for {industry} are coming", _web.Page!.QuerySelector("#industry-coming")?.TextContent.Trim());
        }

        [Then("I see {string} switched on on the built-in frameworks page")]
        public async Task ThenSwitchedOnOnBuiltIn(string name)
        {
            await _web.OpenAsync("/Frameworks/BuiltIn");
            var row = _web.Page!.QuerySelectorAll("#built-in-frameworks tbody tr")
                .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == name);
            Assert.Equal("On", row.QuerySelector(".framework-state")?.TextContent.Trim());
        }

        // Fills the form like a user: the industry in the select, the box ticked or not
        private async Task ChooseAsync(string industry, bool usesAi)
        {
            await _web.OpenAsync(SettingsPath);
            var form = _web.Page!.QuerySelector("form#choose-industry")
                ?? throw new Xunit.Sdk.XunitException("Settings has no industry form.");
            Assert.Contains(industry, form.QuerySelectorAll("select[name=industry] option").Select(o => o.GetAttribute("value")));
            var fields = new Dictionary<string, string> { ["industry"] = industry };
            if (usesAi)
            {
                fields["productsUseAi"] = "true";
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task<IElement> SuggestionAsync(string name)
        {
            await _web.OpenAsync(SettingsPath);
            return Suggestions().FirstOrDefault(s => s.GetAttribute("data-name") == name)
                ?? throw new Xunit.Sdk.XunitException($"The industry does not suggest '{name}'.");
        }

        private IEnumerable<IElement> Suggestions() => _web.Page!.QuerySelectorAll("#industry-suggestions li.industry-suggestion");

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
