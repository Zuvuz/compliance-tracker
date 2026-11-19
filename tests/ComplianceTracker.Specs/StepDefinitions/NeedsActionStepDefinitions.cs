using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-155: requirements that need action. UI contract: Views/Projects/_ComplianceTab.cshtml (form #find-needs-action,
    // #summary-needs-action), _RequirementSummaryRow.cshtml (.requirement-status, .needs-action-priority),
    // _RequirementPage.cshtml (.needs-action-box with .needs-action-priority, .needs-action-reason, .ai-generated,
    // .needs-action-raised and the forms .create-story-for-needs-action and .dismiss-needs-action) and
    // _OverviewTab.cshtml (#needs-attention).
    [Binding]
    public class NeedsActionStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly FakeAi _ai;
        private readonly ComplianceTabStepDefinitions _compliance;

        public NeedsActionStepDefinitions(WebScenario web, FakeAi ai, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _ai = ai;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given / When

        [Given("the AI says {string} applies and needs action with {string} because {string}")]
        public void GivenTheAiSaysItNeedsAction(string code, string priority, string reason)
            => _ai.NeedsAction[code] = (priority, reason);

        [Given("{string} is marked as needing action")]
        public async Task GivenIsMarked(string code)
        {
            GivenTheAiSaysItNeedsAction(code, "High", "the product has remote sign-in");
            await WhenIClickFind("Find requirements that need action", "CRA", Project);
        }

        [When("I click {string} on the {string} sub-tab of {string}")]
        public async Task WhenIClickFind(string button, string subTab, string project)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, project);
            var form = _web.Page!.QuerySelector("#find-needs-action")
                ?? throw new Xunit.Sdk.XunitException("The tab has no \"Find requirements that need action\".");
            Assert.Equal(button, form.QuerySelector("button")!.TextContent.Trim());
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // R5: "Create story" asks the AI for a draft; the draft is created as it stands
        [When("I create the story from the AI's draft on {string}")]
        public async Task WhenICreateTheStory(string code)
        {
            var form = Page(code).QuerySelector("form.create-story-for-needs-action")
                ?? throw new Xunit.Sdk.XunitException($"The page of '{code}' has no \"Create story\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));

            var draft = Page(code).QuerySelector("form.accept-story-draft")
                ?? throw new Xunit.Sdk.XunitException($"The page of '{code}' shows no story draft.");
            var fields = Hidden(draft);
            foreach (var input in draft.QuerySelectorAll("input:not([type=hidden])"))
            {
                fields[input.GetAttribute("name")!] = input.GetAttribute("value") ?? "";
            }

            foreach (var area in draft.QuerySelectorAll("textarea"))
            {
                fields[area.GetAttribute("name")!] = area.TextContent;
            }

            await _web.PostFormAsync(draft.GetAttribute("action")!, fields);
        }

        [Given("I say {string} is not needed because {string}")]
        [When("I say {string} is not needed because {string}")]
        public async Task WhenISayNotNeeded(string code, string reason)
        {
            var form = Page(code).QuerySelector("form.dismiss-needs-action")
                ?? throw new Xunit.Sdk.XunitException($"The page of '{code}' has no \"Not needed\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["reason"] = reason });
        }

        // ---------------------------------------------------------- Then

        [Then("the overview row {string} is {string}")]
        public async Task ThenTheOverviewRowIs(string code, string status)
            => Assert.Equal(status, (await ListRowAsync(code)).QuerySelector(".requirement-status")?.TextContent.Trim());

        [Then("the overview says {string}")]
        public async Task ThenTheOverviewSays(string text)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = db.Projects.Single(p => p.Name == Project).Id;
                await _web.OpenAsync($"/Projects/Details/{id}");
            }

            Assert.Contains(text, _web.Page!.QuerySelectorAll("#needs-attention li").Select(li => Normalize(li.TextContent)));
        }

        [Then("the page of {string} shows {string} and {string} as AI-generated")]
        public void ThenThePageShows(string code, string priority, string reason)
        {
            var box = Box(code);
            Assert.Equal(priority, box.GetAttribute("data-priority"));
            Assert.Equal(reason, box.QuerySelector(".needs-action-reason")?.TextContent.Trim());
            Assert.NotNull(box.QuerySelector(".ai-generated"));
        }

        [Then("the page of {string} says it was raised by a risk")]
        public void ThenRaisedByRisk(string code)
            => Assert.NotNull(Box(code).QuerySelector(".needs-action-raised"));

        [Then("the page of {string} has no {string} or {string}")]
        public void ThenThePageHasNo(string code, string first, string second)
        {
            var box = Box(code);
            var labels = box.QuerySelectorAll("button, summary").Select(b => b.TextContent.Trim()).ToList();
            Assert.DoesNotContain(first, labels);
            Assert.DoesNotContain(second, labels);
        }

        // ---------------------------------------------------------- #52: the project's own stories first

        [When("I ask for stories from the project on the page of {string}")]
        public async Task WhenIAskForStoriesFromTheProject(string code)
        {
            var form = Box(code).QuerySelector("form.suggest-stories-for-requirement")
                ?? throw new Xunit.Sdk.XunitException($"The needs-action box of '{code}' has no \"Suggest stories from the project\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // The form is gone from the page once the requirement is covered; a request sent anyway (an old tab) is refused
        [When("I post {string} for {string} anyway")]
        public async Task WhenIPostAnyway(string button, string code)
        {
            Assert.Equal("Suggest stories from the project", button);
            await using var db = _web.CreateDbContext();
            var project = await db.Projects.SingleAsync(p => p.Name == Project);
            await _compliance.WhenIOpenTheSubTab("CRA", Project);
            var requirementId = Page(code).GetAttribute("data-id")!;
            var token = _web.Page!.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
            await _web.PostFormAsync("/Projects/SuggestStoriesForRequirement", new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["projectId"] = project.Id.ToString(),
                ["requirementId"] = requirementId,
                ["req"] = code
            });
        }

        [Then("the needs-action box of {string} starts with {string} and has {string} under {string}")]
        public void ThenTheBoxStartsWith(string code, string first, string hidden, string more)
        {
            var box = Box(code);
            var buttons = box.QuerySelectorAll("button, summary").Select(b => Normalize(b.TextContent)).ToList();
            Assert.Equal(first, buttons.FirstOrDefault());
            var menu = box.QuerySelector("details.needs-action-more")
                ?? throw new Xunit.Sdk.XunitException($"The needs-action box of '{code}' has no \"{more}\".");
            Assert.Equal(more, Normalize(menu.QuerySelector("summary")!.TextContent));
            Assert.Equal(hidden, Normalize(menu.QuerySelector("form.create-story-for-needs-action button")!.TextContent));
        }

        // ---------------------------------------------------------- Helpers

        // From any page: the Compliance tab's CRA sub-tab first when the requirement is not reachable from here
        private IElement Page(string code)
        {
            if (RequirementRowLookup.Find(_web, code) is { } found)
            {
                return found;
            }

            Task.Run(() => _compliance.WhenIOpenTheSubTab("CRA", Project)).GetAwaiter().GetResult(); // off xUnit's context (no deadlock)
            return RequirementRowLookup.Find(_web, code) ?? throw new Xunit.Sdk.XunitException($"No requirement '{code}'.");
        }

        private IElement Box(string code)
            => Page(code).QuerySelector(".needs-action-box") ?? throw new Xunit.Sdk.XunitException($"'{code}' is not marked as needing action.");

        // The overview as it is now (opened again, so a change made in the background shows)
        private async Task<IElement> ListRowAsync(string code)
        {
            await _compliance.WhenIOpenTheSubTab("CRA", Project);

            return RequirementRowLookup.ListRow(_web, code) ?? throw new Xunit.Sdk.XunitException($"The overview has no row '{code}'.");
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private static string Normalize(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
