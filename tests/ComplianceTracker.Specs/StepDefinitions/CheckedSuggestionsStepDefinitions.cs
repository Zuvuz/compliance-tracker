using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-205: the pretend checking model's answers, the Documents page's li.document-suggestion[data-code] with
    // .passage-check (.check-verdict, .check-passage, .check-missing, .checked-by, .not-checked), and the Evidence tab's
    // .through-story with form.link-directly
    [Binding]
    public class CheckedSuggestionsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public CheckedSuggestionsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("the AI check finds that {string} is partly met by {string} and misses {string}")]
        public void GivenPartlyMet(string code, string passage, string missing)
            => _ai.PassageAnswers[code] = $"VERDICT: Partly meets\nPASSAGE: {passage}\nMISSING: {missing}";

        [Given("the AI check finds that {string} is met by {string}")]
        public void GivenMet(string code, string passage)
            => _ai.PassageAnswers[code] = $"VERDICT: Meets\nPASSAGE: \"{passage}\"\nMISSING: Nothing";

        [Given("the AI check finds {string} and {string} the same")]
        public void GivenTheSame(string from, string to)
            => _ai.MappingAnswers[$"{from}|{to}"] = "VERDICT: Same\nDIFFERS: Nothing";

        [Given("the AI check finds {string} and {string} only related")]
        public void GivenRelated(string from, string to)
            => _ai.MappingAnswers[$"{from}|{to}"] = "VERDICT: Related\nDIFFERS: One is about logs, the other about backups";

        [Then("the document suggestion {string} says {string} with the passage {string}")]
        public async Task ThenTheSuggestionSays(string code, string verdict, string passage)
        {
            var check = await CheckAsync(code);
            Assert.Equal("true", check.GetAttribute("data-checked"));
            Assert.Equal(verdict, check.QuerySelector(".check-verdict")?.TextContent.Trim());
            Assert.Equal($"“{passage}”", check.QuerySelector(".check-passage")?.TextContent.Trim());
        }

        [Then("the document suggestion {string} says what is missing: {string}")]
        public async Task ThenItSaysWhatIsMissing(string code, string missing)
            => Assert.Equal($"Missing: {missing}", (await CheckAsync(code)).QuerySelector(".check-missing")?.TextContent.Trim());

        [Then("the document suggestion {string} is checked by {string}")]
        public async Task ThenCheckedBy(string code, string model)
            => Assert.Equal($"Checked by {model}", (await CheckAsync(code)).QuerySelector(".checked-by")?.TextContent.Trim());

        [Then("the document suggestion {string} is not checked")]
        public async Task ThenNotChecked(string code)
        {
            var check = await CheckAsync(code);
            Assert.Equal("false", check.GetAttribute("data-checked"));
            Assert.Equal("Not checked", check.QuerySelector(".not-checked")?.TextContent.Trim());
        }

        [Then("{string} is not suggested for {string}")]
        public async Task ThenNotSuggested(string document, string code)
        {
            await _web.OpenAsync("/Documents");
            Assert.Empty(_web.Page!.QuerySelectorAll($"li.document-suggestion[data-code='{code}']"));
            Assert.Contains(document, _web.Page!.Body!.TextContent);
        }

        [Given("the evidence {string} of {string} shows the story {string} is done")]
        public async Task GivenTheEvidenceShowsTheStory(string evidence, string project, string story)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            var evidenceId = await db.Evidence.Where(e => e.ProjectId == projectId && e.Title == evidence).Select(e => e.Id).SingleAsync();
            var storyId = await db.WorkItems.Where(w => w.ProjectId == projectId && w.Title == story).Select(w => w.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}?tab=evidence");
            await _web.PostFormAsync("/Projects/LinkEvidenceStory", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["evidenceId"] = evidenceId.ToString(),
                ["workItemId"] = storyId.ToString()
            });
        }

        [When("I link {string} of {string} directly to what it reaches through {string}")]
        public async Task WhenILinkDirectly(string evidence, string project, string story)
        {
            await OpenEvidenceAsync(project);
            var through = Card(evidence).QuerySelectorAll(".through-story").FirstOrDefault(t => t.TextContent.Contains($"Through {story}:", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{evidence}' reaches nothing through '{story}'.");
            var form = through.QuerySelector("form.link-directly") ?? throw new Xunit.Sdk.XunitException("No \"Link directly\".");
            await _web.PostFormAsync(form.GetAttribute("action")!,
                form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                    .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? ""));
        }

        [Then("{string} has the evidence {string} linked to {string}")]
        public async Task ThenLinked(string project, string evidence, string code)
        {
            await OpenEvidenceAsync(project);
            Assert.NotNull(Card(evidence).QuerySelector($".evidence-requirement[data-code='{code}']"));
            Assert.Null(Card(evidence).QuerySelector($".through-story[data-code='{code}']"));
        }

        // R9: on the system owner's AI quality page
        [When("I measure the document suggestions")]
        public async Task WhenIMeasure()
        {
            await _web.OpenAsync("/System/AiQuality");
            var form = _web.Page!.QuerySelector("#evaluate-passage-check") ?? throw new Xunit.Sdk.XunitException("No \"Measure document suggestions\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>());
        }

        [Then("the newest document suggestion run has {int} decision, {string} right when checked, {string} found, and the model {string}")]
        public async Task ThenTheNewestRun(int decisions, string right, string found, string model)
        {
            await _web.OpenAsync("/System/AiQuality");
            var run = _web.Page!.QuerySelectorAll("tr.ai-run[data-step='PassageCheck']").FirstOrDefault()
                ?? throw new Xunit.Sdk.XunitException("No run of the document suggestions.");
            Assert.Equal(decisions.ToString(), run.QuerySelector(".run-decisions")?.TextContent.Trim());
            Assert.Equal(right, run.QuerySelector(".run-checked-right")?.TextContent.Trim());
            Assert.Equal(found, run.QuerySelector(".run-found")?.TextContent.Trim());
            Assert.StartsWith(model, run.QuerySelector(".run-model")?.TextContent.Trim());
        }

        private async Task<AngleSharp.Dom.IElement> CheckAsync(string code)
        {
            await _web.OpenAsync("/Documents");
            var suggestion = _web.Page!.QuerySelector($"li.document-suggestion[data-code='{code}']")
                ?? throw new Xunit.Sdk.XunitException($"No suggestion for '{code}' on the Documents page.");
            return suggestion.QuerySelector(".passage-check") ?? throw new Xunit.Sdk.XunitException($"The suggestion for '{code}' shows no check.");
        }

        private async Task OpenEvidenceAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}?tab=evidence");
        }

        private AngleSharp.Dom.IElement Card(string title)
            => _web.Page!.QuerySelectorAll(".evidence-item").FirstOrDefault(c => c.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Evidence tab has no '{title}'.");
    }
}
