using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-221: the confirmation page for an AI suggestion (/Projects/ConfirmSuggestion). UI contract:
    // Views/Projects/ConfirmSuggestion.cshtml (#suggestion-confirmation, form#confirm-suggestion with #argument-confirmed,
    // a#cancel-suggestion, #suggestion-decided) and Views/Shared/_SuggestionArgument.cshtml (.argument-asks, .argument-quote,
    // .argument-missing, .argument-confidence, .argument-models). "Approve" is sent as the button sends it.
    [Binding]
    public class SuggestionArgumentStepDefinitions
    {
        private const string Project = "Acme Portal";
        private readonly WebScenario _web;

        public SuggestionArgumentStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I click \"Approve\" on the suggestion {string} for {string}")]
        [When("I send the approval of {string} for {string} without the tick")]
        public async Task WhenIClickApprove(string code, string story)
        {
            var (projectId, suggestionId) = await IdsAsync(code, story);
            await _web.OpenAsync($"/Projects/Details/{projectId}/compliance");
            await _web.PostFormAsync("/Projects/ApproveSuggestion", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["suggestionId"] = suggestionId.ToString()
            });
        }

        [When("I confirm that I have read the argument")]
        public async Task WhenIConfirm()
        {
            Assert.NotNull(_web.Page!.QuerySelector("form#confirm-suggestion input#argument-confirmed[required]"));
            await _web.ConfirmArgumentAsync();
        }

        [When("I cancel the confirmation")]
        public async Task WhenICancel()
        {
            var href = _web.Page!.QuerySelector("a#cancel-suggestion")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No Cancel link.");
            await _web.OpenAsync(href);
        }

        [When("I open the confirmation of the suggestion {string} for {string}")]
        public async Task WhenIOpenTheConfirmation(string code, string story)
        {
            var (projectId, suggestionId) = await IdsAsync(code, story);
            await _web.OpenAsync($"/Projects/ConfirmSuggestion?projectId={projectId}&kind=Story&suggestionId={suggestionId}");
        }

        [Then("the argument says the requirement asks {string}")]
        public void ThenAsks(string text) => Assert.Equal(text, Part("asks"));

        [Then("the argument says {string}")]
        public void ThenSays(string text) => Assert.Contains(text, Part("quote"), StringComparison.Ordinal);

        [Then("the argument's confidence is {string}")]
        public void ThenConfidence(string level) => Assert.StartsWith(level, Part("confidence"), StringComparison.Ordinal);

        [Then("I am shown the argument instead")]
        public void ThenShown()
        {
            Assert.Equal("/Projects/ConfirmSuggestion", _web.CurrentPath);
            Assert.NotNull(_web.Page!.QuerySelector("#suggestion-confirmation .suggestion-argument"));
        }

        [Then("the suggestion {string} for {string} still waits")]
        public async Task ThenStillWaits(string code, string story)
        {
            await using var db = _web.CreateDbContext();
            var suggestion = await SuggestionAsync(db, code, story);
            Assert.True(suggestion.IsPending);
        }

        [Then("the confirmation of the suggestion {string} for {string} is kept")]
        public async Task ThenKept(string code, string story)
        {
            await using var db = _web.CreateDbContext();
            Assert.NotNull((await SuggestionAsync(db, code, story)).ArgumentConfirmedAt);
        }

        [Then("the confirmation says it was rejected by {string}")]
        public void ThenDecidedBy(string name)
        {
            var text = _web.Page!.QuerySelector("#suggestion-decided")?.TextContent ?? throw new Xunit.Sdk.XunitException("The page does not say it was decided.");
            Assert.Contains($"rejected by {name}", text, StringComparison.Ordinal);
            Assert.Null(_web.Page.QuerySelector("form#confirm-suggestion"));
        }

        private string Part(string name)
            => _web.Page!.QuerySelector($"#suggestion-confirmation .argument-{name}")?.TextContent.Trim()
               ?? throw new Xunit.Sdk.XunitException($"The argument has no part '{name}'.");

        private async Task<(Guid ProjectId, Guid SuggestionId)> IdsAsync(string code, string story)
        {
            await using var db = _web.CreateDbContext();
            var suggestion = await SuggestionAsync(db, code, story);
            return (suggestion.ProjectId, suggestion.Id);
        }

        private static Task<Domain.Mappings.MappingSuggestion> SuggestionAsync(Infrastructure.Persistence.ComplianceTrackerDbContext db, string code, string story)
            => (from s in db.MappingSuggestions
                join w in db.WorkItems on s.WorkItemId equals w.Id
                join r in db.Requirements on s.RequirementId equals r.Id
                join p in db.Projects on s.ProjectId equals p.Id
                where w.Title == story && r.Code == code && p.Name == Project
                orderby s.CreatedAt descending
                select s).FirstAsync();
    }
}
