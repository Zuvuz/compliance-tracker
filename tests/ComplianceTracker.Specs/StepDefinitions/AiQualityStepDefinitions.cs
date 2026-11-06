using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-116: the AI quality page, as the system admin, with the pretend AI.
    // UI contract: Views/System/AiQuality.cshtml (#evaluate-story-matching, #ai-runs rows .ai-run with data-step and
    // .run-decisions, .run-top1, .run-model, .run-grade; #ai-comparison li; #grade-rationales with .rationale-sample).
    [Binding]
    public class AiQualityStepDefinitions
    {
        private const string Page = "/System/AiQuality";

        private readonly WebScenario _web;
        private readonly FakeAi _ai;
        private readonly AiSuggestionsStepDefinitions _suggestions;

        public AiQualityStepDefinitions(WebScenario web, FakeAi ai, AiSuggestionsStepDefinitions suggestions)
        {
            _web = web;
            _ai = ai;
            _suggestions = suggestions;
        }

        [Given("the rationale of {string} for {string} has been written")]
        public Task GivenTheRationaleHasBeenWritten(string code, string story)
            => _suggestions.ThenTheRationaleBecomes(code, story, $"Fake rationale for {code}");

        [Given("I have approved the suggestion {string} for {string}")]
        public Task GivenIHaveApproved(string code, string story)
            => _suggestions.WhenIApproveTheSuggestion(code, story);

        // The organisation's admin switches it on, as on the Settings page (US-036)
        [Given("{string} shares its decisions to improve the AI")]
        public Task GivenSharesItsDecisions(string organisation)
            => _suggestions.WhenISwitchOnSharing();

        [Given("a measurement needs at least {int} decisions")]
        public void GivenAMeasurementNeeds(int decisions)
            => _ai.MinimumDecisionsForEvaluation = decisions;

        [Given("I have measured story matching")]
        [When("I measure story matching")]
        public async Task WhenIMeasureStoryMatching()
        {
            await _web.OpenAsync(Page);
            var form = _web.Page!.QuerySelector("#evaluate-story-matching")
                ?? throw new Xunit.Sdk.XunitException("The AI quality page has no \"Measure story matching\" button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>());
        }

        [When("I grade the rationale of {string} with {int}")]
        public async Task WhenIGradeTheRationale(string code, int grade)
        {
            await _web.OpenAsync(Page);
            var form = _web.Page!.QuerySelector("#grade-rationales")
                ?? throw new Xunit.Sdk.XunitException("The AI quality page has no rationales to grade.");
            var row = form.QuerySelectorAll(".rationale-sample").FirstOrDefault(r => r.GetAttribute("data-code") == code)
                ?? throw new Xunit.Sdk.XunitException($"No rationale for '{code}' to grade.");
            var name = row.QuerySelector("select")!.GetAttribute("name")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { [name] = grade.ToString() });
        }

        [Then("the newest story matching run has {int} decision, top 1 {string} and the model {string}")]
        public async Task ThenTheNewestMatchingRun(int decisions, string top1, string model)
        {
            var run = await NewestAsync("StoryMatching");
            Assert.Equal(decisions.ToString(), run.QuerySelector(".run-decisions")?.TextContent.Trim());
            Assert.Equal(top1, run.QuerySelector(".run-top1")?.TextContent.Trim());
            Assert.StartsWith(model, run.QuerySelector(".run-model")?.TextContent.Trim());
        }

        [Then("the newest rationale run has the grade {string} and the model {string}")]
        public async Task ThenTheNewestRationaleRun(string grade, string model)
        {
            var run = await NewestAsync("Rationale");
            Assert.Equal(grade, run.QuerySelector(".run-grade")?.TextContent.Trim());
            Assert.StartsWith(model, run.QuerySelector(".run-model")?.TextContent.Trim());
        }

        [Then("the comparison says {string}")]
        public async Task ThenTheComparisonSays(string text)
        {
            await _web.OpenAsync(Page);
            Assert.Contains(text, _web.Page!.QuerySelectorAll("#ai-comparison li").Select(li => li.TextContent.Trim()));
        }

        private async Task<IElement> NewestAsync(string step)
        {
            await _web.OpenAsync(Page);
            return _web.Page!.QuerySelectorAll("#ai-runs .ai-run").FirstOrDefault(r => r.GetAttribute("data-step") == step)
                ?? throw new Xunit.Sdk.XunitException($"The AI quality page has no {step} run.");
        }
    }
}
