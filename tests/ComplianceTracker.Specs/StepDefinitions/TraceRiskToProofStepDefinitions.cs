using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-146: the evidence card's trace, evidence linked to stories, the AI's story suggestions and a locked report as
    // evidence, through the pages like a user. UI contract: Views/Projects/_EvidenceTab.cshtml (.evidence-card[data-title]
    // with .trace-risk, .evidence-requirement, .trace-story (data-title, .trace-via), form.link-evidence-story,
    // .evidence-story-suggestion[data-story] with form.approve-evidence-story-suggestion), _WorkItems.cshtml
    // (.work-item-evidence) and _ReportsTab.cshtml (form.use-report-as-evidence).
    [Binding]
    public class TraceRiskToProofStepDefinitions
    {
        private const string Project = "Acme Portal";
        private readonly WebScenario _web;
        private readonly EvidenceTabStepDefinitions _evidence;
        private string? _card;

        public TraceRiskToProofStepDefinitions(WebScenario web, EvidenceTabStepDefinitions evidence)
        {
            _web = web;
            _evidence = evidence;
        }

        // ---------------------------------------------------------- Given / When

        [Given("the evidence {string} proves {string}")]
        public async Task GivenTheEvidenceProves(string title, string code)
        {
            await _evidence.GivenTheEvidenceIsOnTheTab(title, Project);
            var form = Card(title).QuerySelector("form.link-evidence")!;
            var option = form.QuerySelectorAll("option").First(o => o.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal));
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["requirementIds"] = option.GetAttribute("value")! });
        }

        [When("I open the evidence {string}")]
        public async Task WhenIOpenTheEvidence(string title)
        {
            await OpenTabAsync("evidence");
            _card = title;
            Card(title);
        }

        [When("I link the evidence {string} to {string}")]
        public async Task WhenILinkTheEvidenceToTheStory(string title, string story)
        {
            await OpenTabAsync("evidence");
            var form = Card(title).QuerySelector("form.link-evidence-story") ?? throw new Xunit.Sdk.XunitException("No story can be linked.");
            var option = form.QuerySelectorAll("option").Single(o => o.TextContent.Trim() == story);
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["workItemId"] = option.GetAttribute("value")! });
        }

        [When("I approve the story {string} for {string}")]
        public async Task WhenIApproveTheStory(string story, string title)
        {
            await OpenTabAsync("evidence");
            var form = StorySuggestion(title, story).QuerySelector("form.approve-evidence-story-suggestion")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
            await WhenIOpenTheEvidence(title);
        }

        [When("I use the locked report as evidence for {string}")]
        public async Task WhenIUseTheLockedReportAsEvidence(string code)
        {
            await OpenTabAsync("reports");
            var form = _web.Page!.QuerySelector("form.use-report-as-evidence") ?? throw new Xunit.Sdk.XunitException("No locked report can be used as evidence.");
            var option = form.QuerySelectorAll("option").First(o => o.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal));
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["requirementId"] = option.GetAttribute("value")! });
        }

        // ---------------------------------------------------------- Then

        [Then("its trace shows the risk {string} {string}")]
        public void ThenItsTraceShowsTheRisk(string risk, string via)
        {
            var item = Card(_card!).QuerySelectorAll(".trace-risk").SingleOrDefault(r => r.GetAttribute("data-title") == risk)
                ?? throw new Xunit.Sdk.XunitException($"The trace has no risk '{risk}'.");
            Assert.Equal(via, item.QuerySelector(".trace-via")?.TextContent.Trim());
        }

        [Then("its trace shows the requirement {string}")]
        public void ThenItsTraceShowsTheRequirement(string code)
            => Assert.Contains(Card(_card!).QuerySelectorAll(".evidence-requirement"), r => r.GetAttribute("data-code") == code);

        [Then("its trace shows the story {string} {string}")]
        public void ThenItsTraceShowsTheStory(string story, string via)
        {
            var item = Card(_card!).QuerySelectorAll(".trace-story").SingleOrDefault(r => r.GetAttribute("data-title") == story)
                ?? throw new Xunit.Sdk.XunitException($"The trace has no story '{story}'.");
            Assert.Equal(via, item.QuerySelector(".trace-via")?.TextContent.Trim());
        }

        [Then("the story {string} shows the evidence {string}")]
        public async Task ThenTheStoryShowsTheEvidence(string story, string title)
        {
            await OpenTabAsync("product-requirements");
            var card = _web.Page!.QuerySelectorAll("[data-work-item-title], .work-item, li, article, div.card")
                .FirstOrDefault(c => c.TextContent.Contains(story) && c.QuerySelector(".work-item-evidence") is not null)
                ?? throw new Xunit.Sdk.XunitException($"The story '{story}' shows no evidence line.");
            Assert.Contains(title, card.QuerySelector(".work-item-evidence")!.TextContent);
        }

        [Then("{string} waits for my decision on {string}")]
        public async Task ThenWaitsForMyDecision(string story, string title)
        {
            await OpenTabAsync("evidence");
            Assert.NotNull(StorySuggestion(title, story).QuerySelector("form.approve-evidence-story-suggestion"));
        }

        [Then("the evidence {string} proves {string}")]
        public async Task ThenTheEvidenceProvesTheRequirement(string title, string code)
        {
            await OpenTabAsync("evidence");
            Assert.Contains(Card(title).QuerySelectorAll(".evidence-requirement"), r => r.GetAttribute("data-code") == code);
        }

        // ---------------------------------------------------------- Helpers

        private IElement Card(string title)
            => _web.Page!.QuerySelectorAll("[data-title]").FirstOrDefault(c => c.GetAttribute("data-title") == title && c.QuerySelector(".fold-part") is not null)
               ?? throw new Xunit.Sdk.XunitException($"The Evidence tab has no card '{title}'.");

        private IElement StorySuggestion(string title, string story)
            => Card(title).QuerySelectorAll(".evidence-story-suggestion").SingleOrDefault(s => s.GetAttribute("data-story") == story)
               ?? throw new Xunit.Sdk.XunitException($"'{title}' has no story suggestion '{story}'.");

        private async Task OpenTabAsync(string tab)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/{tab}");
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
