using AngleSharp.Html.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-024: editing a project and changing its lifecycle phase, through the web app with test sign-in.
    // The steps define the UI contract: the form on /Projects/Edit/{id} (#edit-project), and on the
    // project page the Edit link (#edit-project-link), the phase form (#change-phase) and #project-phase.
    [Binding]
    public class EditProjectsStepDefinitions
    {
        private readonly WebScenario _web;

        public EditProjectsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("{string} is in phase {string}")]
        public Task GivenIsInPhase(string projectName, string phase)
            => WhenIChangeThePhase(projectName, phase);

        // ---------------------------------------------------------- When

        [When("I change the scope of {string} to {string}")]
        public Task WhenIChangeTheScope(string projectName, string scope)
            => EditAsync(projectName, "Scope", scope);

        [When("I rename {string} to {string}")]
        public Task WhenIRename(string projectName, string newName)
            => EditAsync(projectName, "Name", newName);

        [When("I change the intended purpose of {string} to {string}")]
        public Task WhenIChangeTheIntendedPurpose(string projectName, string purpose)
            => EditAsync(projectName, "IntendedPurpose", purpose);

        // Sends the phase form. Also when the page hides it (R4), so the test proves the server refuses it too.
        [When("I change the phase of {string} to {string}")]
        public async Task WhenIChangeThePhase(string projectName, string phase)
        {
            var id = await ProjectIdAsync(projectName);
            await _web.OpenAsync($"/Projects/Details/{id}");
            await _web.PostFormAsync("/Projects/ChangePhase", new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["phase"] = phase
            });
        }

        [When("{string} opens the edit page of {string}")]
        public async Task WhenOpensTheEditPageOf(string userName, string projectName)
        {
            var id = await ProjectIdAsync(projectName);
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync($"/Projects/Edit/{id}");
        }

        // ---------------------------------------------------------- Then

        [Then("the page of {string} shows scope {string}")]
        public async Task ThenThePageShowsScope(string projectName, string scope)
        {
            await OpenDetailsAsync(projectName);
            Assert.Equal(scope, _web.Page!.QuerySelector("#project-scope")?.TextContent.Trim());
        }

        [Then("the page of {string} shows phase {string}")]
        public async Task ThenThePageShowsPhase(string projectName, string phase)
        {
            await OpenDetailsAsync(projectName);
            Assert.Equal(phase, _web.Page!.QuerySelector("#project-phase")?.TextContent.Trim());
        }

        [Then("the page of {string} has no Edit button and no phase form")]
        public async Task ThenThePageHasNoEditButton(string projectName)
        {
            await OpenDetailsAsync(projectName);
            Assert.Null(_web.Page!.QuerySelector("#edit-project-link"));
            Assert.Null(_web.Page!.QuerySelector("#change-phase"));
            Assert.NotNull(_web.Page!.QuerySelector("#project-closed"));
        }

        // R3: the newest entry of this kind holds the old and new value
        [Then("the {string} entry says {string}")]
        public async Task ThenTheEntrySays(string action, string details)
        {
            await using var db = _web.CreateDbContext();
            var expectedAction = Enum.Parse<ActivityAction>(action);

            var entries = await db.ActivityLog
                .Where(e => e.Action == expectedAction)
                .Select(e => e.Details)
                .ToListAsync();

            Assert.Contains(details, entries);
        }

        // ---------------------------------------------------------- Helpers

        // Opens the edit form like a user, changes one field and keeps the others as the form shows them.
        // If the form is not offered (R4), the request is sent anyway, as a direct request would be.
        private async Task EditAsync(string projectName, string field, string value)
        {
            var id = await ProjectIdAsync(projectName);
            await _web.OpenAsync($"/Projects/Edit/{id}");

            var fields = new Dictionary<string, string> { ["Id"] = id.ToString() };
            foreach (var name in new[] { "Name", "Description", "IntendedPurpose", "Scope" })
            {
                fields[name] = FieldValue(name);
            }

            fields[field] = value;
            await _web.PostFormAsync("/Projects/Edit", fields);
        }

        private string FieldValue(string name)
        {
            var element = _web.Page!.QuerySelector($"#edit-project [name='{name}']");
            return element switch
            {
                IHtmlInputElement input => input.Value,
                IHtmlTextAreaElement area => area.Value,
                _ => ""
            };
        }

        private async Task OpenDetailsAsync(string projectName)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}");

        // Project names are unique per organisation, and these scenarios have one organisation with projects
        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
