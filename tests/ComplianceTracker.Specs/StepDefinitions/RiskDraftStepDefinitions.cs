using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-112: the Risks tab's form#draft-risks, #risks-drafted, and #risk-drafts .risk-draft[data-title] with
    // .risk-draft-based-on and the forms .accept-risk-draft and .discard-risk-draft; #risk-ai-drafted on the risk page
    [Binding]
    public class RiskDraftStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public RiskDraftStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("I ask the AI to draft risks for {string}")]
        [When("I ask the AI to draft risks for {string}")]
        public async Task WhenIAskForDrafts(string project)
        {
            await OpenAsync(project);
            var form = _web.Page!.QuerySelector("form#draft-risks") ?? throw new Xunit.Sdk.XunitException("The Risks tab has no \"Draft risks with AI\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I accept the draft {string} as {string} with likelihood {int} and impact {int}")]
        public async Task WhenIAccept(string draft, string title, int likelihood, int impact)
        {
            await OpenAsync("Acme Portal");
            var form = Draft(draft).QuerySelector("form.accept-risk-draft")!;
            var fields = Hidden(form);
            fields["title"] = title;
            fields["description"] = "From the draft";
            fields["likelihood"] = likelihood.ToString();
            fields["impact"] = impact.ToString();
            fields["ownerId"] = form.QuerySelector("select[name=ownerId] option")!.GetAttribute("value")!;
            fields["treatment"] = "Reduce";
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Given("I discard the draft {string} because {string}")]
        public async Task GivenIDiscard(string draft, string reason)
        {
            await OpenAsync("Acme Portal");
            var form = Draft(draft).QuerySelector("form.discard-risk-draft")!;
            var fields = Hidden(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the Risks tab of {string} has the drafts {string} and {string}")]
        public async Task ThenHasTheDrafts(string project, string first, string second)
        {
            await OpenAsync(project);
            Assert.Equal(new[] { first, second }.Order(), Titles().Order());
        }

        [Then("the Risks tab of {string} has only the draft {string}")]
        public async Task ThenHasOnlyTheDraft(string project, string title)
        {
            await OpenAsync(project);
            Assert.Equal([title], Titles());
        }

        [Then("the draft {string} says it is based on {string}")]
        public void ThenTheDraftSaysItIsBasedOn(string draft, string basedOn)
            => Assert.Equal($"Based on: {basedOn}", Draft(draft).QuerySelector(".risk-draft-based-on")!.TextContent.Trim());

        [Then("the agent was told the intended purpose and the uncovered requirements")]
        public void ThenTheAgentWasTold()
        {
            var prompt = _ai.Prompts.Last(p => p.Contains(ComplianceTracker.Application.Risks.RiskDraftPrompt.Marker, StringComparison.Ordinal));
            Assert.Contains("Intended purpose: Test purpose", prompt);
            Assert.Contains("CRA I.2(d)", prompt);
        }

        [Then("the risk {string} is marked AI-drafted")]
        public async Task ThenTheRiskIsAiDrafted(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
            Assert.NotNull(_web.Page!.QuerySelector("#risk-ai-drafted"));
        }

        [Then("the AI says it left out {string} because the risk {string} covers it")]
        public void ThenTheAiSaysItLeftOut(string draft, string risk)
            => Assert.Contains($"Left out: \"{draft}\" (the risk \"{risk}\" covers it)", _web.Page!.QuerySelector("#risks-drafted")?.TextContent ?? "", StringComparison.Ordinal);

        private IEnumerable<string> Titles() => _web.Page!.QuerySelectorAll("#risk-drafts .risk-draft").Select(d => d.GetAttribute("data-title")!);

        private IElement Draft(string title)
            => _web.Page!.QuerySelectorAll("#risk-drafts .risk-draft").FirstOrDefault(d => d.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Risks tab has no draft '{title}'.");

        private async Task OpenAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/risks");
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
