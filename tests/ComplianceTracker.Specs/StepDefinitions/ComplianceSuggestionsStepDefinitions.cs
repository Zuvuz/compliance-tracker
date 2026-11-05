using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-044: the summary, the filters and the AI suggestions on the Compliance tab, through the web app with
    // test sign-in and a pretend AI (FakeAi).
    // UI contract: Views/Projects/_ComplianceTab.cshtml (#summary-covered, #summary-total, #summary-not-covered,
    // #summary-suggestions, #summary-not-applicable, #compliance-filters a[data-filter], form #suggest-not-covered,
    // rows .requirement-row with .ai-suggestion[data-story] and form .approve-suggestion) and on the Product requirements tab
    // .ai-suggestion-badge.
    [Binding]
    public class ComplianceSuggestionsStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly FakeAi _ai;
        private readonly ComplianceTabStepDefinitions _compliance;

        public ComplianceSuggestionsStepDefinitions(WebScenario web, FakeAi ai, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _ai = ai;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given

        [Given("the AI suggests {string} for {string}")]
        public void GivenTheAiSuggests(string story, string code)
            => _ai.Rate(story, code, 0.9);

        [Given("the row {string} on the {string} sub-tab has the suggestion {string}")]
        public async Task GivenTheRowHasTheSuggestion(string code, string subTab, string story)
        {
            GivenTheAiSuggests(story, code);
            await WhenIAskForSuggestions(subTab, Project);
            ThenTheRowShowsTheSuggestion(code, story);
        }

        // ---------------------------------------------------------- When

        // Opens the sub-tab and clicks the filter, like a user
        [When("I open the {string} sub-tab of {string} showing {string}")]
        public async Task WhenIOpenTheSubTabShowing(string subTab, string projectName, string filter)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, projectName);
            var link = _web.Page!.QuerySelectorAll("#compliance-filters a[data-filter]").FirstOrDefault(a => a.TextContent.Trim() == filter)
                ?? throw new Xunit.Sdk.XunitException($"The Compliance tab has no filter '{filter}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I ask for suggestions on the {string} sub-tab of {string}")]
        public async Task WhenIAskForSuggestions(string subTab, string projectName)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, projectName);
            var form = _web.Page!.QuerySelector("form#suggest-not-covered")
                ?? throw new Xunit.Sdk.XunitException("The sub-tab has no \"Suggest for not covered\" button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I approve the suggestion on the row {string}")]
        public async Task WhenIApproveTheSuggestionOnTheRow(string code)
        {
            var form = Row(code).QuerySelector("form.approve-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no Approve button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        // Another member, with their own sign-in; the page is kept for the Then steps
        [When("{string} opens the {string} sub-tab of {string}")]
        public async Task WhenOpensTheSubTab(string userName, string subTab, string projectName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();

            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{id}/compliance"));
            var link = page.QuerySelectorAll("#compliance-tabs a[data-sub]").FirstOrDefault(a => a.TextContent.Trim() == subTab)
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' sees no sub-tab '{subTab}'.");
            _web.Response = await client.GetAsync(link.GetAttribute("href")!);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        // ---------------------------------------------------------- Then

        [Then("the summary shows {int} covered of {int}, {int} not covered and {int} not applicable")]
        public void ThenTheSummaryShows(int covered, int total, int notCovered, int notApplicable)
        {
            Assert.Equal(covered.ToString(), Text("#summary-covered"));
            Assert.Equal(total.ToString(), Text("#summary-total"));
            Assert.Equal(notCovered.ToString(), Text("#summary-not-covered"));
            Assert.Equal(notApplicable.ToString(), Text("#summary-not-applicable"));
        }

        [Then("the summary shows AI suggestions")]
        public void ThenTheSummaryShowsAiSuggestions()
            => Assert.True(int.Parse(Text("#summary-suggestions")) > 0, "The summary counts no AI suggestions.");

        [Then("the row {string} is not listed")]
        public void ThenTheRowIsNotListed(string code)
            => Assert.Null(_web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']"));

        [Then("the row {string} is listed")]
        public void ThenTheRowIsListed(string code)
            => Row(code);

        [Then("the row {string} shows the suggestion {string}")]
        public void ThenTheRowShowsTheSuggestion(string code, string story)
            => Assert.Contains(story, Row(code).QuerySelectorAll(".ai-suggestion").Select(s => s.GetAttribute("data-story")));

        [Then("no suggestion is saved for {string}")]
        public async Task ThenNoSuggestionIsSavedFor(string code)
        {
            await using var db = _web.CreateDbContext();
            var count = await (
                    from s in db.MappingSuggestions
                    join r in db.Requirements on s.RequirementId equals r.Id
                    where r.Code == code
                    select s)
                .CountAsync();
            Assert.Equal(0, count);
        }

        [Then("no suggestions are saved")]
        public async Task ThenNoSuggestionsAreSaved()
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(0, await db.MappingSuggestions.CountAsync());
        }

        [Then("the user story {string} links to its AI suggestions on the {string} sub-tab")]
        public async Task ThenTheUserStoryLinksToItsSuggestions(string story, string subTab)
        {
            var item = _web.Page!.QuerySelectorAll(".work-item")
                .Single(i => i.QuerySelector(".work-item-title")?.TextContent.Trim() == story);
            var badge = item.QuerySelector("a.ai-suggestion-badge")
                ?? throw new Xunit.Sdk.XunitException($"'{story}' has no AI suggestions badge.");
            Assert.Matches(@"^\d+ AI suggestions?$", badge.TextContent.Trim());

            await _web.OpenAsync(badge.GetAttribute("href")!);
            Assert.Equal([subTab], _web.Page!.QuerySelectorAll("#compliance-tabs a.active").Select(a => a.TextContent.Trim()));
            Assert.Equal("Has suggestions", _web.Page!.QuerySelector("#compliance-filters a[aria-current='true']")?.TextContent.Trim());
            Assert.Contains(story, _web.Page!.QuerySelectorAll(".ai-suggestion").Select(s => s.GetAttribute("data-story")));
        }

        // ---------------------------------------------------------- Helpers

        private string Text(string selector)
            => _web.Page!.QuerySelector(selector)?.TextContent.Trim()
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no {selector}.");

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} lists no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
