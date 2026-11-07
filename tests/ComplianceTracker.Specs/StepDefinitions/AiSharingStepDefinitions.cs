using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-130: sharing AI decisions by default. UI contract: Views/Settings/Index.cshtml (#ai-sharing-state,
    // #set-ai-sharing, #ai-shared-count), Views/Organizations/Create.cshtml (#ai-sharing-notice) and
    // Views/Projects/Edit.cshtml (#project-ai-sharing, #project-ai-sharing-state).
    [Binding]
    public class AiSharingStepDefinitions
    {
        private readonly WebScenario _web;

        public AiSharingStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("{string} does not share its decisions")]
        public async Task GivenDoesNotShare(string organisation)
        {
            await _web.OpenAsync("/Settings");
            if (_web.Page!.QuerySelector("#ai-sharing-state")?.TextContent.Trim() == "Off")
            {
                return;
            }

            var form = _web.Page!.QuerySelector("form#set-ai-sharing")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
            Assert.Equal("Off", _web.Page!.QuerySelector("#ai-sharing-state")?.TextContent.Trim());
        }

        [When("I open the page to create an organisation")]
        public Task WhenIOpenTheCreatePage() => _web.OpenAsync("/Organizations/Create");

        [When("I open the Settings page")]
        public Task WhenIOpenTheSettingsPage() => _web.OpenAsync("/Settings");

        [When("I leave {string} out of AI improvement")]
        public async Task WhenILeaveTheProjectOut(string project)
        {
            await OpenEditAsync(project);
            var form = _web.Page!.QuerySelector("form#project-ai-sharing")
                ?? throw new Xunit.Sdk.XunitException("The Edit page has no AI improvement switch.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Then("I am told that AI decisions are shared by default")]
        public void ThenIAmTold()
            => Assert.Contains("shared by default", _web.Page!.QuerySelector("#ai-sharing-notice")?.TextContent);

        [Then("{string} shares its AI decisions")]
        public async Task ThenSharesItsDecisions(string organisation)
        {
            await using var db = _web.CreateDbContext();
            Assert.True(await db.Organizations.Where(o => o.Name == organisation).Select(o => o.ShareAiFeedback).SingleAsync());
        }

        [Then("the Settings page says {string}")]
        public async Task ThenTheSettingsPageSays(string text)
        {
            await _web.OpenAsync("/Settings");
            Assert.Equal(text, _web.Page!.QuerySelector("#ai-shared-count")?.TextContent.Trim());
        }

        [Then("the Edit page of {string} says AI improvement is {string}")]
        public async Task ThenTheEditPageSays(string project, string state)
        {
            await OpenEditAsync(project);
            Assert.Equal(state, _web.Page!.QuerySelector("#project-ai-sharing-state")?.TextContent.Trim());
        }

        private async Task OpenEditAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Edit/{id}");
        }

        private static Dictionary<string, string> Hidden(AngleSharp.Dom.IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
