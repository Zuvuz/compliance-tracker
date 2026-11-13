using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-118: the pretend reranker and the two scores of a suggestion. UI contract: the Compliance tab's
    // tr.requirement-row[data-code] with .ai-suggestion[data-story] (.suggestion-score, .suggestion-search-score), and the
    // Evidence tab's .evidence-item[data-title] .evidence-suggestion[data-code] (.suggestion-search-score).
    [Binding]
    public class RerankingStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly FakeAi _ai;

        public RerankingStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance, FakeAi ai)
        {
            _web = web;
            _compliance = compliance;
            _ai = ai;
        }

        [Given("a reranker is configured")]
        public void GivenARerankerIsConfigured() => _ai.RerankerOn = true;

        [Given("the reranker scores {string} against {string} with {float}")]
        public void GivenTheRerankerScores(string title, string code, double score) => _ai.RerankScores[(title, code)] = score;

        [Then("the suggestions on {string} are {string} and then {string}")]
        public async Task ThenTheSuggestionsAre(string code, string first, string second)
            => Assert.Equal([first, second], (await RowAsync(code)).QuerySelectorAll(".ai-suggestion").Select(s => s.GetAttribute("data-story")));

        [Then("the suggestion {string} on {string} shows the score {string} and {string}")]
        public async Task ThenTheSuggestionShowsBoth(string story, string code, string score, string search)
        {
            var suggestion = await SuggestionAsync(story, code);
            Assert.Equal(score, suggestion.QuerySelector(".suggestion-score")?.TextContent.Trim());
            Assert.Equal(search, suggestion.QuerySelector(".suggestion-search-score")?.TextContent.Trim());
        }

        [Then("the suggestion {string} on {string} shows only the score {string}")]
        public async Task ThenTheSuggestionShowsOnly(string story, string code, string score)
        {
            var suggestion = await SuggestionAsync(story, code);
            Assert.Equal(score, suggestion.QuerySelector(".suggestion-score")?.TextContent.Trim());
            Assert.Null(suggestion.QuerySelector(".suggestion-search-score"));
        }

        [Then("the evidence suggestion {string} of {string} says {string}")]
        public async Task ThenTheEvidenceSuggestionSays(string code, string title, string text)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/evidence");
            }

            var suggestion = _web.Page!.QuerySelectorAll("#evidence-list .evidence-item").FirstOrDefault(e => e.GetAttribute("data-title") == title)
                ?.QuerySelectorAll(".evidence-suggestion").FirstOrDefault(s => s.GetAttribute("data-code") == code)
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no suggestion '{code}'.");
            Assert.Equal(text, suggestion.QuerySelector(".suggestion-search-score")?.TextContent.Trim());
        }

        [Then("the story suggestion {string} of {string} shows the score {string} and {string}")]
        public async Task ThenTheStorySuggestionShows(string story, string title, string score, string search)
        {
            var suggestion = (await EvidenceItemAsync(title)).QuerySelectorAll(".evidence-story-suggestion").FirstOrDefault(s => s.GetAttribute("data-story") == story)
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no story suggestion '{story}'.");
            Assert.Equal(score, suggestion.QuerySelector(".suggestion-score")?.TextContent.Trim());
            Assert.Equal(search, suggestion.QuerySelector(".suggestion-search-score")?.TextContent.Trim());
        }

        private async Task<IElement> EvidenceItemAsync(string title)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/evidence");
            }

            return _web.Page!.QuerySelectorAll("#evidence-list .evidence-item").FirstOrDefault(e => e.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"The Evidence tab does not list '{title}'.");
        }

        private async Task<IElement> RowAsync(string code)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            return _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")!;
        }

        private async Task<IElement> SuggestionAsync(string story, string code)
            => (await RowAsync(code)).QuerySelectorAll(".ai-suggestion").FirstOrDefault(s => s.GetAttribute("data-story") == story)
               ?? throw new Xunit.Sdk.XunitException($"'{code}' has no suggestion '{story}'.");
    }
}
