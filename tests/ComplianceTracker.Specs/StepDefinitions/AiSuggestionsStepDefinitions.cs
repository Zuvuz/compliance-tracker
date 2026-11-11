using AngleSharp.Dom;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-020, US-036: AI suggestions through the web app with test sign-in and a pretend AI (FakeAi).
    // UI contract: on the Product requirements tab (_ProductRequirementsTab.cshtml, _WorkItems.cshtml) #suggest-mappings and per story
    // .show-more-suggestions; on the Compliance tab (_ComplianceTab.cshtml, US-044) .ai-suggestion with data-story and
    // data-code, .suggestion-score, .suggestion-rationale and the forms .approve-suggestion and .reject-suggestion
    // (reason, comment); on the Settings page #set-ai-sharing and #ai-sharing-state.
    [Binding]
    public class AiSuggestionsStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public AiSuggestionsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        // ---------------------------------------------------------- Given

        [Given("the AI rates {string} against {string} with {float}")]
        public void GivenTheAiRates(string story, string code, float score)
            => _ai.Rate(story, code, score);

        // Scores 0.40, 0.35, 0.30 ... in the requirements' order, so "best first" is known
        [Given("the AI rates {string} against every requirement of {string}")]
        public async Task GivenTheAiRatesEveryRequirement(string story, string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var codes = await (
                    from r in db.Requirements
                    join v in db.FrameworkVersions on r.FrameworkVersionId equals v.Id
                    join f in db.Frameworks on v.FrameworkId equals f.Id
                    where f.Name == frameworkName
                    orderby r.Position
                    select r.Code)
                .ToListAsync();

            for (var i = 0; i < codes.Count; i++)
            {
                _ai.Rate(story, codes[i], 0.40 - i * 0.05);
            }
        }

        // Uploads the framework with N requirements (BP-1 ... BP-N) added by hand, then activates it
        [Given("the framework {string} has {int} active requirements")]
        public async Task GivenTheFrameworkHasActiveRequirements(string frameworkName, int count)
        {
            await _web.OpenAsync("/Frameworks");
            await _web.PostFileAsync("/Frameworks/Add", new Dictionary<string, string> { ["Name"] = frameworkName },
                "framework.pdf", TestFiles.Pdf());
            var versionId = FrameworkUi.OpenVersionId(_web);
            for (var i = 1; i <= count; i++)
            {
                await FrameworkUi.AddRequirementAsync(_web, versionId, $"BP-{i}", $"Big policy rule {i}");
            }

            await FrameworkUi.ActivateAsync(_web, versionId);
        }

        [Given("the local AI is not available")]
        public void GivenTheLocalAiIsNotAvailable()
            => _ai.Unavailable = true;

        // ---------------------------------------------------------- When

        [Given("I have clicked \"Suggest mappings\" for {string}")]
        [When("I click \"Suggest mappings\" for {string}")]
        public async Task WhenIClickSuggestMappings(string projectName)
        {
            await OpenUserStoriesAsync(projectName);
            var form = _web.Page!.QuerySelector("form#suggest-mappings")
                ?? throw new Xunit.Sdk.XunitException("The Product requirements tab has no \"Suggest mappings\" button.");
            await _web.PostFormAsync("/Projects/SuggestMappings", HiddenFields(form));
        }

        [When("I ask for more suggestions for {string}")]
        public async Task WhenIAskForMoreSuggestions(string story)
        {
            await OpenUserStoriesAsync(Project);
            var form = Story(story).QuerySelector("form.show-more-suggestions")
                ?? throw new Xunit.Sdk.XunitException($"'{story}' has no \"Show more\" button.");
            await _web.PostFormAsync("/Projects/ShowMoreSuggestions", HiddenFields(form));
        }

        [When("I approve the suggestion {string} for {string}")]
        public async Task WhenIApproveTheSuggestion(string code, string story)
        {
            var form = (await SuggestionAsync(story, code)).QuerySelector("form.approve-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"The suggestion '{code}' has no Approve button.");
            await _web.PostFormAsync("/Projects/ApproveSuggestion", HiddenFields(form));
        }

        [Given("I reject the suggestion {string} for {string} with the reason {string} and the comment {string}")]
        [When("I reject the suggestion {string} for {string} with the reason {string} and the comment {string}")]
        public async Task WhenIRejectTheSuggestion(string code, string story, string reason, string comment)
        {
            var form = (await SuggestionAsync(story, code)).QuerySelector("form.reject-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"The suggestion '{code}' has no Reject form.");
            var fields = HiddenFields(form);
            fields["reason"] = reason;
            fields["comment"] = comment;
            await _web.PostFormAsync("/Projects/RejectSuggestion", fields);
        }

        [When("I switch on \"Share our decisions to improve the AI\"")]
        public async Task WhenISwitchOnSharing()
        {
            await _web.OpenAsync("/Settings");
            if (_web.Page!.QuerySelector("#ai-sharing-state")?.TextContent.Trim() == "On")
            {
                return; // US-130: on by default
            }

            var form = _web.Page!.QuerySelector("form#set-ai-sharing")
                ?? throw new Xunit.Sdk.XunitException("The organisation page has no AI sharing switch.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
            Assert.Equal("On", _web.Page!.QuerySelector("#ai-sharing-state")?.TextContent.Trim());
        }

        // A file comes back when it works; otherwise the organisation page with the error
        // ---------------------------------------------------------- Then

        [Then("I see the suggestion {string} for {string} with score {string} marked {string}")]
        public async Task ThenISeeTheSuggestion(string code, string story, string score, string mark)
        {
            var suggestion = await SuggestionAsync(story, code);
            Assert.Equal(score, suggestion.QuerySelector(".suggestion-score")?.TextContent.Trim());
            Assert.Contains(mark, suggestion.QuerySelectorAll(".badge").Select(b => b.TextContent.Trim()));
        }

        // Step 2 runs in the background, so the page is opened again until the rationale is there (at most 10 s)
        [Then("the rationale of {string} for {string} becomes {string}")]
        public async Task ThenTheRationaleBecomes(string code, string story, string rationale)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                if ((await SuggestionAsync(story, code)).QuerySelector(".suggestion-rationale")?.TextContent.Trim() == rationale)
                {
                    return;
                }

                await Task.Delay(200);
            }

            Assert.Fail($"The rationale of {code} never became '{rationale}'.");
        }

        [Then("{string} has {int} suggestions, best first")]
        public async Task ThenHasSuggestionsBestFirst(string story, int count)
        {
            var scores = (await SuggestionsAsync(story)).Select(s => double.Parse(s.QuerySelector(".suggestion-score")!.TextContent, CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(count, scores.Count);
            Assert.Equal(scores.OrderByDescending(s => s), scores);
        }

        [Then("{string} has {int} suggestions with no requirement twice")]
        public async Task ThenHasSuggestionsWithNoRequirementTwice(string story, int count)
        {
            var codes = (await SuggestionsAsync(story)).Select(s => s.GetAttribute("data-code")).ToList();
            Assert.Equal(count, codes.Count);
            Assert.Equal(count, codes.Distinct().Count());
        }

        [Then("{string} has no suggestion {string}")]
        public async Task ThenHasNoSuggestion(string story, string code)
        {
            Assert.DoesNotContain(code, (await SuggestionsAsync(story)).Select(s => s.GetAttribute("data-code")));
        }

        [Then("the suggestion {string} for {string} is no longer shown")]
        public Task ThenTheSuggestionIsNoLongerShown(string code, string story)
            => ThenHasNoSuggestion(story, code);

        [Then("{string} is linked to {string} with the source {string}")]
        public async Task ThenIsLinkedWithTheSource(string story, string code, string source)
        {
            var links = await LinksAsync(story, code);
            Assert.Equal([Enum.Parse<LinkSource>(source)], links.Select(l => l.Source));
        }

        [Then("{string} is not linked to {string}")]
        public async Task ThenIsNotLinked(string story, string code)
            => Assert.Empty(await LinksAsync(story, code));

        [Then("the rejection of {string} for {string} is kept with {string}, {string} and {string}")]
        public async Task ThenTheRejectionIsKept(string code, string story, string userName, string reason, string comment)
        {
            await using var db = _web.CreateDbContext();
            var suggestion = await SuggestionsQuery(db, story, code).SingleAsync();
            var anna = (await db.Organizations.SingleAsync()).Users.Single(u => u.Name == userName);

            Assert.Equal(SuggestionStatus.Rejected, suggestion.Status);
            Assert.Equal(Enum.Parse<RejectionReason>(reason), suggestion.RejectionReason);
            Assert.Equal(comment, suggestion.Comment);
            Assert.Equal(anna.Id, suggestion.DecidedById);
            Assert.NotNull(suggestion.DecidedAt);
        }

        [Then("each suggestion keeps its score, rank, model {string} and prompt version {string}")]
        public async Task ThenEachSuggestionKeeps(string model, string promptVersion)
        {
            await using var db = _web.CreateDbContext();
            var suggestions = await db.MappingSuggestions.ToListAsync();
            Assert.NotEmpty(suggestions);
            Assert.All(suggestions, s =>
            {
                Assert.InRange(s.Score, 0, 1);
                Assert.True(s.Rank >= 1);
                Assert.Equal(model, s.EmbeddingModel);
                Assert.Equal(promptVersion, s.PromptVersion);
            });
        }

        // R7: no field about the person who decided, and no e-mail addresses. Names written into a story's own
        // text are part of the story and stay.
        // ---------------------------------------------------------- Helpers

        private async Task OpenUserStoriesAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/product-requirements");
        }

        private IElement Story(string title)
            => _web.Page!.QuerySelectorAll(".work-item").FirstOrDefault(i => i.QuerySelector(".work-item-title")?.TextContent.Trim() == title)
               ?? throw new Xunit.Sdk.XunitException($"The Product requirements tab shows no story '{title}'.");

        // US-044: the story's pending suggestions on every sub-tab of the Compliance tab, in the order shown
        private async Task<IReadOnlyList<IElement>> SuggestionsAsync(string story)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/compliance");
            }

            var found = new List<IElement>();
            foreach (var href in _web.Page!.QuerySelectorAll("#compliance-tabs a[data-sub]:not([data-sub='all'])").Select(a => a.GetAttribute("href")!).ToList())
            {
                await _web.OpenAsync(href);
                found.AddRange(_web.Page!.QuerySelectorAll(".ai-suggestion").Where(s => s.GetAttribute("data-story") == story));
            }

            return found;
        }

        private async Task<IElement> SuggestionAsync(string story, string code)
            => (await SuggestionsAsync(story)).FirstOrDefault(s => s.GetAttribute("data-code") == code)
               ?? throw new Xunit.Sdk.XunitException($"'{story}' has no suggestion '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private static IQueryable<MappingSuggestion> SuggestionsQuery(Infrastructure.Persistence.ComplianceTrackerDbContext db, string story, string code)
            => from s in db.MappingSuggestions
               join w in db.WorkItems on s.WorkItemId equals w.Id
               join r in db.Requirements on s.RequirementId equals r.Id
               where w.Title == story && r.Code == code
               select s;

        private async Task<IReadOnlyList<RequirementLink>> LinksAsync(string story, string code)
        {
            await using var db = _web.CreateDbContext();
            return await (
                    from l in db.RequirementLinks
                    join w in db.WorkItems on l.WorkItemId equals w.Id
                    join r in db.Requirements on l.RequirementId equals r.Id
                    where w.Title == story && r.Code == code && l.RemovedAt == null
                    select l)
                .ToListAsync();
        }
    }
}
