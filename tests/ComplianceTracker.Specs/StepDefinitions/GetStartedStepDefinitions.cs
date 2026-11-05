using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-096: the "Get started" card on /Projects (Views/Projects/_GetStarted.cshtml: #get-started,
    // .get-started-step with .get-started-status and a.get-started-link, #hide-get-started, #show-get-started)
    [Binding]
    public class GetStartedStepDefinitions
    {
        private readonly WebScenario _web;
        private IDocument? _otherPage;

        public GetStartedStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I hide the {string} card")]
        public Task WhenIHideTheCard(string _)
            => PostAsync("#hide-get-started");

        [When("I show the {string} card again")]
        public Task WhenIShowTheCardAgain(string _)
            => PostAsync("#show-get-started");

        [When("{string} opens the projects page")]
        public async Task WhenOpensTheProjectsPage(string name)
        {
            using var client = _web.CreateClient(signedInAs: name);
            _otherPage = await WebScenario.ParseAsync(await client.GetAsync("/Projects"));
        }

        [Then("the {string} card shows {string} as {string}")]
        public async Task ThenTheCardShows(string _, string step, string status)
        {
            await EnsureProjectsPageAsync();
            Assert.Equal(status, Step(_web.Page!, step).QuerySelector(".get-started-status")?.TextContent.Trim());
        }

        [Then("the step {string} links to {string}")]
        public void ThenTheStepLinksTo(string step, string path)
            => Assert.Equal(path, Step(_web.Page!, step).QuerySelector("a.get-started-link")?.GetAttribute("href"));

        [Then("the {string} card is not shown")]
        public async Task ThenTheCardIsNotShown(string _)
        {
            await EnsureProjectsPageAsync();
            Assert.Null(_web.Page!.QuerySelector("#get-started"));
            Assert.NotNull(_web.Page!.QuerySelector("#show-get-started"));
        }

        // R5: the steps are shown, but the Auditor gets no links to do them
        [Then("her {string} card has no links")]
        public void ThenHerCardHasNoLinks(string _)
        {
            Assert.NotNull(_otherPage!.QuerySelector("#get-started"));
            Assert.Empty(_otherPage!.QuerySelectorAll("#get-started a.get-started-link"));
        }

        private async Task PostAsync(string formSelector)
        {
            await _web.OpenAsync("/Projects");
            var form = _web.Page!.QuerySelector(formSelector)
                ?? throw new Xunit.Sdk.XunitException($"/Projects has no {formSelector}.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>());
        }

        private async Task EnsureProjectsPageAsync()
        {
            if (_web.Response is null || _web.CurrentPath != "/Projects")
            {
                await _web.OpenAsync("/Projects");
            }
        }

        private static IElement Step(IDocument page, string title)
            => page.QuerySelectorAll("#get-started .get-started-step")
                   .FirstOrDefault(s => s.TextContent.Contains(title, StringComparison.Ordinal))
               ?? throw new Xunit.Sdk.XunitException($"The \"Get started\" card has no step \"{title}\".");
    }
}
