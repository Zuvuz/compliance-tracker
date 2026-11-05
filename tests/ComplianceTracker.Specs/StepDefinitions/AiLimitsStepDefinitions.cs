using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-105: the limits are held by the pretend AI (FakeAi implements IAiLimits) for one scenario. The Evidence tab
    // shows the limit (.ai-limit) and why there are no suggestions (#evidence-ai-unavailable).
    [Binding]
    public class AiLimitsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public AiLimitsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("the minimum score for evidence suggestions is {float}")]
        public void GivenTheMinimumScoreForEvidence(double score)
            => _ai.MinimumScoreForEvidence = score;

        [Given("the minimum score for story suggestions is {float}")]
        public void GivenTheMinimumScoreForStories(double score)
            => _ai.MinimumScoreForStories = score;

        [Then("the Evidence tab says {string}")]
        public void ThenTheEvidenceTabSays(string text)
        {
            var shown = (_web.Page!.QuerySelector("#evidence-ai-unavailable")?.TextContent ?? "")
                        + (_web.Page!.QuerySelector(".ai-limit")?.TextContent ?? "");
            Assert.Contains(text, shown);
        }

        [Then("the suggestion {string} for {string} was made with the limit {float}")]
        public async Task ThenTheSuggestionWasMadeWithTheLimit(string code, string title, double limit)
        {
            await using var db = _web.CreateDbContext();
            var evidenceId = await db.Evidence.Where(e => e.Title == title).Select(e => e.Id).SingleAsync();
            var requirementIds = await db.Requirements.Where(r => r.Code == code).Select(r => r.Id).ToListAsync();
            var suggestion = await db.EvidenceLinkSuggestions.SingleAsync(s => s.EvidenceId == evidenceId && requirementIds.Contains(s.RequirementId));
            Assert.Equal(limit, suggestion.MinimumScore, 3);
        }
    }
}
