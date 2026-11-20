using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-040: the management report's texts on the Reports tab, with the pretend AI (FakeAi.StatusSummary).
    // UI contract: Views/Projects/_ReportsTab.cshtml (#status-texts, form.draft-status-texts, .status-text[data-part] with
    // .status-text-body, .ai-generated and .outdated, form.accept-status-text, form.edit-status-text).
    [Binding]
    public class StatusReportTextsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public StatusReportTextsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("the AI writes the management summary {string}")]
        public void GivenTheAiWritesTheSummary(string summary) => _ai.StatusSummary = summary;

        [Given("I have drafted the management report texts of {string}")]
        [When("I draft the management report texts of {string}")]
        public async Task WhenIDraft(string project)
        {
            await OpenAsync(project);
            var form = _web.Page!.QuerySelector("#status-texts form.draft-status-texts")
                ?? throw new Xunit.Sdk.XunitException("The Reports tab has no button to draft the texts.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Given("I have accepted every management report text")]
        public async Task GivenIHaveAcceptedEvery()
        {
            await OpenAsync();
            for (var i = 0; i < 20 && _web.Page!.QuerySelector("#status-texts form.accept-status-text") is { } form; i++)
            {
                await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
            }

            Assert.Null(_web.Page!.QuerySelector("#status-texts form.accept-status-text"));
        }

        [When("I edit the management report text {string} to {string}")]
        public async Task WhenIEdit(string part, string text)
        {
            await OpenAsync();
            var form = Text(part).QuerySelector("form.edit-status-text")
                ?? throw new Xunit.Sdk.XunitException($"'{part}' cannot be edited.");
            var fields = Hidden(form);
            fields["text"] = text;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the management report text {string} is {string}")]
        public async Task ThenTheTextIs(string part, string text)
        {
            await OpenAsync();
            Assert.Equal(text, Text(part).QuerySelector(".status-text-body")!.TextContent.Trim());
        }

        [Then("the management report text {string} is marked {string}")]
        public async Task ThenTheTextIsMarked(string part, string mark)
        {
            await OpenAsync();
            Assert.Contains(mark, Marks(part));
        }

        [Then("the management report text {string} is not marked {string}")]
        public async Task ThenTheTextIsNotMarked(string part, string mark)
        {
            await OpenAsync();
            Assert.DoesNotContain(mark, Marks(part));
        }

        [Then("the management report texts say {string}")]
        public void ThenTheReportsTabSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#status-texts")?.TextContent ?? "");

        [Then("there are no management report texts")]
        public async Task ThenThereAreNoTexts()
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(0, await db.StatusTexts.CountAsync());
        }

        [Then("{string} cannot accept the management report text {string} of {string}")]
        public async Task ThenCannotAccept(string userName, string part, string project)
        {
            var projectId = await ProjectIdAsync(project);
            await using var db = _web.CreateDbContext();
            var text = await db.StatusTexts.SingleAsync(t => t.ProjectId == projectId && t.Part == "summary");
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{projectId}/reports"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;

            await client.PostAsync("/Projects/AcceptStatusText", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["statusTextId"] = text.Id.ToString(),
                ["__RequestVerificationToken"] = token
            }));

            await using var after = _web.CreateDbContext();
            Assert.True((await after.StatusTexts.SingleAsync(t => t.Id == text.Id)).IsAiGenerated, $"{userName} accepted '{part}'.");
        }

        // ---------------------------------------------------------- Helpers

        private async Task OpenAsync(string project = "Acme Portal")
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/reports");

        private IElement Text(string part)
            => _web.Page!.QuerySelectorAll("#status-texts .status-text").FirstOrDefault(t => t.GetAttribute("data-part") == part)
               ?? throw new Xunit.Sdk.XunitException($"The Reports tab shows no text '{part}'.");

        private IReadOnlyList<string> Marks(string part)
            => Text(part).QuerySelectorAll(".ai-generated, .outdated").Select(b => b.TextContent.Trim()).ToList();

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
