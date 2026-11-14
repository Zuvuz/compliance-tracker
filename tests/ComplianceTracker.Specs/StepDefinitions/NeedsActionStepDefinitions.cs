using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

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
            await WhenIClickFind("Find what needs action", "CRA", Project);
        }

        [When("I click {string} on the {string} sub-tab of {string}")]
        public async Task WhenIClickFind(string button, string subTab, string project)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, project);
            var form = _web.Page!.QuerySelector("#find-needs-action")
                ?? throw new Xunit.Sdk.XunitException("The tab has no \"Find what needs action\".");
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
