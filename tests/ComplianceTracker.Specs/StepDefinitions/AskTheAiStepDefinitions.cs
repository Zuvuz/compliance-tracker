using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-106: "Ask the AI" on a requirement row, the story draft, and "Suggest measures" on a risk, through the pages like a
    // user. UI contract: Views/Projects/_RequirementRow.cshtml (form.ask-the-ai, .ai-suggestion[data-story],
    // .ai-evidence-suggestion[data-evidence] with form.approve-row-evidence-suggestion, .story-draft[data-title] with
    // form.accept-story-draft and form.discard-story-draft), _ComplianceTab.cshtml (#new-issue-address) and
    // Views/Risks/Details.cshtml (form#suggest-measures, .measure-suggestion[data-measure] with form.approve-measure).
    [Binding]
    public class AskTheAiStepDefinitions
    {
        private const string Project = "Acme Portal";
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private string? _code;

        public AskTheAiStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I asked the AI for suggestions for {string}")]
        [When("I ask the AI for suggestions for {string}")]
        public async Task WhenIAskTheAi(string code)
        {
            _code = code;
            await _compliance.OpenSubTabWithAsync(Project, code);
            var form = Row(code).QuerySelector("form.ask-the-ai") ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no \"Ask the AI\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
            await _compliance.OpenSubTabWithAsync(Project, code);
        }

        [When("I approve the suggested evidence {string} on the row {string}")]
        public async Task WhenIApproveTheSuggestedEvidence(string title, string code)
        {
            var form = Row(code).QuerySelectorAll(".ai-evidence-suggestion").Single(s => s.GetAttribute("data-evidence") == title)
                .QuerySelector("form.approve-row-evidence-suggestion")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
            await _compliance.OpenSubTabWithAsync(Project, code);
        }

        [When("I change the draft's rules to {string} and create the story")]
        public async Task WhenIChangeTheDraftAndCreateTheStory(string rules)
        {
            var form = Draft().QuerySelector("form.accept-story-draft")!;
            var fields = Hidden(form);
            foreach (var input in form.QuerySelectorAll("input:not([type=hidden])"))
            {
                fields[input.GetAttribute("name")!] = input.GetAttribute("value") ?? "";
            }

            foreach (var area in form.QuerySelectorAll("textarea"))
            {
                fields[area.GetAttribute("name")!] = area.TextContent;
            }

            fields["rules"] = rules;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I discard the draft because {string}")]
        public async Task WhenIDiscardTheDraft(string reason)
        {
            var form = Draft().QuerySelector("form.discard-story-draft")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["reason"] = reason });
            await _compliance.OpenSubTabWithAsync(Project, _code!);
        }

        // R5: the issue a person made from the draft is synced from the pretend GitHub
        [When("the issue {string} comes in with the next sync")]
        public Task WhenTheIssueComesIn(string title) => _compliance.GivenTheProjectHasTheUserStory(Project, title);

        [When("I ask the AI to suggest measures for the risk {string}")]
        public async Task WhenIAskForMeasures(string risk)
        {
            await OpenRiskAsync(risk);
            var form = _web.Page!.QuerySelector("form#suggest-measures") ?? throw new Xunit.Sdk.XunitException("The risk has no \"Suggest measures\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I approve the suggested measure {string}")]
        public async Task WhenIApproveTheMeasure(string code)
        {
            var form = _web.Page!.QuerySelectorAll(".measure-suggestion").FirstOrDefault(m => m.GetAttribute("data-measure")!.StartsWith(code + " ", StringComparison.Ordinal))
                ?.QuerySelector("form.approve-measure") ?? throw new Xunit.Sdk.XunitException($"'{code}' is not suggested.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // ---------------------------------------------------------- Then

        [Then("the row {string} suggests the story {string}")]
        public void ThenTheRowSuggestsTheStory(string code, string story)
            => Assert.Contains(Row(code).QuerySelectorAll(".ai-suggestion"), s => s.GetAttribute("data-story") == story);

        [Then("the row {string} suggests the evidence {string}")]
        public void ThenTheRowSuggestsTheEvidence(string code, string title)
            => Assert.Contains(Row(code).QuerySelectorAll(".ai-evidence-suggestion"), s => s.GetAttribute("data-evidence") == title);

        [Then("the row {string} has the evidence {string}")]
        public void ThenTheRowHasTheEvidence(string code, string title)
            => Assert.Contains(Row(code).QuerySelectorAll(".evidence"), e => e.GetAttribute("data-title") == title);

        [Then("the row {string} has a draft user story {string} marked {string}")]
        public void ThenTheRowHasADraft(string code, string title, string badge)
        {
            var draft = Row(code).QuerySelector(".story-draft") ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no draft.");
            Assert.Equal(title, draft.GetAttribute("data-title"));
            Assert.Equal(badge, draft.QuerySelector(".ai-generated")?.TextContent.Trim());
        }

        [Then("the row {string} has no draft")]
        public void ThenTheRowHasNoDraft(string code) => Assert.Null(Row(code).QuerySelector(".story-draft"));

        [Then("the page offers to create the issue in GitHub")]
        public async Task ThenThePageOffersTheIssue()
        {

            var link = _web.Page!.QuerySelector("#new-issue-address")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No link to create the issue.");
            Assert.StartsWith("https://github.com/acme/portal/issues/new?title=Meet%20I.2%28m%29", link);
            Assert.Contains("Every%20account%20can%20be%20deleted", link);
        }

        [Then("{string} is covered by the story {string}")]
        public async Task ThenIsCoveredByTheStory(string code, string story)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            Assert.Contains(Row(code).QuerySelectorAll(".linked-story"), s => s.TextContent.Trim() == story);
            Assert.Contains("AI-drafted", Row(code).QuerySelector(".row-group[data-group='stories'] .item-source")?.TextContent ?? "");
        }

        [Then("the risk {string} has the measure {string}")]
        public async Task ThenTheRiskHasTheMeasure(string risk, string code)
        {
            await using var db = _web.CreateDbContext();
            var measures = await db.Risks.Where(r => r.Title == risk).SelectMany(r => r.Measures).ToListAsync();
            var requirementIds = measures.Select(m => m.RequirementId).OfType<Guid>().ToList();
            Assert.Contains(code, await db.Requirements.Where(r => requirementIds.Contains(r.Id)).Select(r => r.Code).ToListAsync());
        }

        // ---------------------------------------------------------- Helpers

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The Compliance tab has no row '{code}'.");

        private IElement Draft() => Row(_code!).QuerySelector(".story-draft") ?? throw new Xunit.Sdk.XunitException("No draft.");

        private async Task OpenRiskAsync(string risk)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
