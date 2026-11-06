using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-111: treatment actions on the risk page, through the web app with test sign-in and a pretend GitHub.
    // UI contract: Views/Risks/Details.cshtml (#risk-actions li.risk-action with data-title, .risk-action-status,
    // li.risk-action-link with data-kind, the forms .link-risk-action and .complete-risk-action, and #add-risk-action).
    [Binding]
    public class RiskActionsStepDefinitions
    {
        private const string Repository = "acme/portal";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public RiskActionsStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        [Given("I have added the action {string} to the risk {string}")]
        public Task GivenIHaveAddedTheAction(string action, string risk)
            => AddAsync(action, risk, null);

        [Given("I have added the action {string} to the risk {string} due {string}")]
        [When("I add the action {string} to the risk {string} due {string}")]
        public Task WhenIAddTheActionDue(string action, string risk, string due)
            => AddAsync(action, risk, DateOnly.ParseExact(due, "dd-MM-yyyy"));

        [Given("I have linked the action {string} of {string} to the story {string}")]
        [When("I link the action {string} of {string} to the story {string}")]
        public Task WhenILinkTheActionToTheStory(string action, string risk, string story)
            => LinkAsync(action, risk, story);

        [Given("I have linked the action {string} of {string} to the evidence {string}")]
        public Task GivenIHaveLinkedTheActionToTheEvidence(string action, string risk, string evidence)
            => LinkAsync(action, risk, evidence);

        // The story is closed in the pretend GitHub and the project is synced, like a real close
        [Given("the user story {string} is closed")]
        public async Task GivenTheUserStoryIsClosed(string story)
        {
            _github.Repository(Repository).Single(i => i.Title == story).State = "closed";
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == "Acme Portal").Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}/product-requirements");
            await _web.PostFormAsync("/Projects/SyncNow", new Dictionary<string, string> { ["projectId"] = projectId.ToString() });
        }

        [When("I mark the action {string} of {string} as done")]
        public async Task WhenIMarkTheActionAsDone(string action, string risk)
        {
            await OpenAsync(risk);
            var form = Action(action).QuerySelector("form.complete-risk-action")
                ?? throw new Xunit.Sdk.XunitException($"The action '{action}' has no \"Mark as done\" button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [Then("the action {string} of {string} is {string}")]
        public async Task ThenTheActionIs(string action, string risk, string status)
        {
            await OpenAsync(risk);
            Assert.Equal(status, Action(action).QuerySelector(".risk-action-status")?.TextContent.Trim());
        }

        [Then("the action {string} of {string} traces to the story {string}")]
        public async Task ThenTheActionTracesToTheStory(string action, string risk, string story)
        {
            await OpenAsync(risk);
            Assert.Contains(Action(action).QuerySelectorAll("li.risk-action-link[data-kind='story'] a"), a => a.TextContent.Trim() == story);
        }

        [Then("the risk action log of {string} has {string}")]
        public async Task ThenTheRiskActionLogHas(string project, string details)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.Contains(details, await db.ActivityLog
                .Where(e => e.SubjectId == projectId && e.Action == ActivityAction.RiskActionDone)
                .Select(e => e.Details)
                .ToListAsync());
        }

        // ---------------------------------------------------------- Helpers

        private async Task AddAsync(string action, string risk, DateOnly? due)
        {
            await OpenAsync(risk);
            var form = _web.Page!.QuerySelector("#add-risk-action")
                ?? throw new Xunit.Sdk.XunitException($"The risk '{risk}' has no form to add an action.");
            var fields = HiddenFields(form);
            fields["title"] = action;
            fields["ownerId"] = (form.QuerySelector("select[name=ownerId] option[selected]") ?? form.QuerySelector("select[name=ownerId] option"))!
                .GetAttribute("value")!;
            fields["dueOn"] = due?.ToString("yyyy-MM-dd") ?? "";
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // Chooses the story or evidence in the action's list, by its text
        private async Task LinkAsync(string action, string risk, string target)
        {
            await OpenAsync(risk);
            var form = Action(action).QuerySelector("form.link-risk-action")
                ?? throw new Xunit.Sdk.XunitException($"The action '{action}' cannot be linked.");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim() == target)
                ?? throw new Xunit.Sdk.XunitException($"'{target}' cannot be linked to '{action}'.");
            var fields = HiddenFields(form);
            fields["target"] = option.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task OpenAsync(string risk)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        private IElement Action(string title)
            => _web.Page!.QuerySelectorAll("#risk-actions li.risk-action").FirstOrDefault(li => li.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The risk page has no action '{title}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
