using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-136 R2: the AI's suggestions on the Documents page, through the web app like a user.
    // UI contract: Views/Documents/Index.cshtml (#document-ai, li.organisation-document[data-title] with
    // li.document-suggestion[data-code], form.approve-document-suggestion, form.reject-document-suggestion,
    // form.more-document-suggestions).
    [Binding]
    public class DocumentSuggestionsStepDefinitions
    {
        private readonly WebScenario _web;

        public DocumentSuggestionsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- When

        [When("I approve the AI suggestion {string} for {string}")]
        public async Task WhenIApprove(string code, string title)
        {
            var form = (await SuggestionAsync(title, code)).QuerySelector("form.approve-document-suggestion")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I reject the AI suggestion {string} for {string} because {string}")]
        public async Task WhenIReject(string code, string title, string reason)
        {
            var form = (await SuggestionAsync(title, code)).QuerySelector("form.reject-document-suggestion")!;
            var fields = Hidden(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I reject the AI suggestion {string} for {string} without a reason")]
        public async Task WhenIRejectWithoutAReason(string code, string title)
        {
            var form = (await SuggestionAsync(title, code)).QuerySelector("form.reject-document-suggestion")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I ask the AI for more requirements for {string}")]
        public async Task WhenIAskForMore(string title)
        {
            await OpenAsync();
            var form = Document(title).QuerySelector("form.more-document-suggestions")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no way to ask the AI for more.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // The page offers no buttons to this role; the approval is sent anyway, so the server must refuse it
        [When("I send an approval of the AI suggestion {string} for {string} anyway")]
        public async Task WhenISendAnApprovalAnyway(string code, string title)
        {
            using var anna = _web.CreateClient(signedInAs: "Anna");
            var page = await WebScenario.ParseAsync(await anna.GetAsync("/Documents"));
            var suggestionId = page.QuerySelectorAll("li.organisation-document").First(d => d.GetAttribute("data-title") == title)
                .QuerySelectorAll("li.document-suggestion").First(s => s.GetAttribute("data-code") == code)
                .QuerySelector("form.approve-document-suggestion input[name=suggestionId]")!.GetAttribute("value")!;

            await _web.OpenAsync("/Projects"); // a page this role may open, for the form token
            await _web.PostFormAsync("/Documents/ApproveSuggestion", new Dictionary<string, string> { ["suggestionId"] = suggestionId });
        }

        // ---------------------------------------------------------- Then

        [Then("the AI says on the Documents page {string}")]
        public void ThenTheAiSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#document-ai")?.TextContent ?? "", StringComparison.Ordinal);

        [Then("{string} has the AI suggestions {string} and {string}")]
        public async Task ThenHasTheSuggestions(string title, string first, string second)
            => Assert.Equal([first, second], await CodesAsync(title));

        [Then("{string} has the AI suggestions {string}")]
        public async Task ThenHasTheSuggestion(string title, string code)
            => Assert.Equal([code], await CodesAsync(title));

        [Then("{string} has no AI suggestions")]
        public async Task ThenHasNoSuggestions(string title) => Assert.Empty(await CodesAsync(title));

        // ---------------------------------------------------------- Helpers

        // The suggested codes over the minimum score, in the order shown (best first)
        private async Task<List<string>> CodesAsync(string title)
        {
            await OpenAsync();
            return Document(title).QuerySelectorAll("li.document-suggestion").Select(s => s.GetAttribute("data-code")!).ToList();
        }

        private async Task<IElement> SuggestionAsync(string title, string code)
        {
            await OpenAsync();
            return Document(title).QuerySelectorAll("li.document-suggestion").FirstOrDefault(s => s.GetAttribute("data-code") == code)
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no AI suggestion '{code}'.");
        }

        private IElement Document(string title)
            => _web.Page!.QuerySelectorAll("li.organisation-document").FirstOrDefault(d => d.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Documents page has no '{title}'.");

        private Task OpenAsync() => _web.OpenAsync("/Documents");

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
