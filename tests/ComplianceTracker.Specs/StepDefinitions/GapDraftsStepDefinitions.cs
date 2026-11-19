using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-108: form#draft-for-gaps under "Ask the AI…" on a regulation's Compliance tab, and the drafts page
    // (/Projects/Drafts/{id}): .draft[data-form][data-covers] with .ai-generated, .draft-fits, form.create-draft and
    // form.discard-draft, and .waiting-draft with .draft-copy-text
    [Binding]
    public class GapDraftsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public GapDraftsStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("the AI finds that {string} does not fit the intended purpose")]
        public void GivenItDoesNotFit(string code) => _ai.NotApplicable[code] = "The portal has no network of its own";

        [Given("the AI puts {string} and {string} in one draft")]
        public void GivenOneDraft(string first, string second) => _ai.GapGroups.Add([first, second]);

        [Given("I have asked the AI to draft for the gaps in the CRA of {string}")]
        [When("I ask the AI to draft for the gaps in the CRA of {string}")]
        public async Task WhenIAskForDrafts(string project)
        {
            await WhenIOpenTheCra(project);
            var form = _web.Page!.QuerySelector("form#draft-for-gaps") ?? throw new Xunit.Sdk.XunitException("No \"Draft for the gaps\".");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        [When("I open the CRA of {string}")]
        public async Task WhenIOpenTheCra(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance/cra");

        [When("I change the extensions of the draft covering {string} to {string} and create it")]
        public async Task WhenIChangeAndCreate(string code, string extensions)
        {
            var form = (await DraftAsync(code)).QuerySelector("form.create-draft") ?? throw new Xunit.Sdk.XunitException("The draft cannot be created.");
            var fields = form.QuerySelectorAll("input[name], textarea[name]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.LocalName == "textarea" ? i.TextContent : i.GetAttribute("value") ?? "");
            fields["examples"] = extensions;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        [Then("there is a draft {string} covering {string} marked {string}")]
        public async Task ThenThereIsADraft(string form, string code, string mark)
        {
            var draft = await DraftAsync(code);
            Assert.Equal(form, draft.GetAttribute("data-form"));
            Assert.Equal(mark, draft.QuerySelector(".ai-generated")?.TextContent.Trim());
        }

        [Then("the draft covering {string} says how it fits the product")]
        public async Task ThenItFits(string code)
            => Assert.StartsWith("Fits the product: ", (await DraftAsync(code)).QuerySelector(".draft-fits")?.TextContent.Trim());

        [Then("the draft covering {string} has a primary actor and a main success scenario")]
        public async Task ThenItIsAUseCase(string code)
        {
            var draft = await DraftAsync(code);
            Assert.False(string.IsNullOrWhiteSpace(draft.QuerySelector("input[name=role]")?.GetAttribute("value")));
            Assert.StartsWith("1. ", draft.QuerySelector("textarea[name=story]")?.TextContent.Trim());
            Assert.NotNull(draft.QuerySelector("textarea[name=postconditions]"));
        }

        [Then("there is no draft covering {string}")]
        public async Task ThenNoDraft(string code)
        {
            await OpenDraftsAsync();
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll(".draft"), d => Covers(d, code));
        }

        [Then("{string} of {string} has a \"not applicable\" suggestion saying {string}")]
        public async Task ThenNotApplicableSuggested(string code, string project, string reason)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await ProjectIdAsync(project);
            var requirementIds = await db.Requirements.Where(r => r.Code == code).Select(r => r.Id).ToListAsync();
            var suggestion = await db.ApplicabilitySuggestions.Where(s => s.ProjectId == projectId && requirementIds.Contains(s.RequirementId)).SingleAsync();
            Assert.Equal((SuggestionStatus.Pending, reason), (suggestion.Status, suggestion.Reason));
        }

        [Then("the created draft covering {string} waits for the team's document with {string}")]
        public async Task ThenItWaits(string code, string text)
        {
            await OpenDraftsAsync();
            var waiting = _web.Page!.QuerySelectorAll(".waiting-draft").FirstOrDefault(d => Covers(d, code))
                ?? throw new Xunit.Sdk.XunitException($"No created draft covering '{code}' waits.");
            Assert.Contains(text, waiting.QuerySelector(".draft-copy-text")?.TextContent);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll(".draft"), d => Covers(d, code));
        }

        [Then("{string} and {string} of {string} are covered by {string}")]
        public async Task ThenCoveredBy(string first, string second, string project, string title)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await ProjectIdAsync(project);
            var storyId = await db.WorkItems.Where(w => w.ProjectId == projectId && w.Title == title).Select(w => w.Id).SingleAsync();
            foreach (var code in new[] { first, second })
            {
                var requirementIds = await db.Requirements.Where(r => r.Code == code).Select(r => r.Id).ToListAsync();
                Assert.True(await db.RequirementLinks.AnyAsync(l => l.ProjectId == projectId && l.WorkItemId == storyId && requirementIds.Contains(l.RequirementId)
                    && l.RemovedAt == null && l.Source == LinkSource.AiDrafted), $"{code} is not covered by '{title}'.");
            }
        }

        private static bool Covers(AngleSharp.Dom.IElement draft, string code)
            => (draft.GetAttribute("data-covers") ?? "").Split(", ").Any(c => c.EndsWith(" " + code, StringComparison.Ordinal) || c == code);

        private async Task<AngleSharp.Dom.IElement> DraftAsync(string code)
        {
            await OpenDraftsAsync();
            return _web.Page!.QuerySelectorAll(".draft").FirstOrDefault(d => Covers(d, code))
                ?? throw new Xunit.Sdk.XunitException($"No draft covers '{code}'.");
        }

        private async Task OpenDraftsAsync()
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == "Acme Portal").Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Drafts/{projectId}");
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
