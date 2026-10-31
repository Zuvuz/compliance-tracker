using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-031: a private GitHub repository read with an encrypted token, through the web app and FakeGitHub.
    // UI contract: the forms /Projects/SetGitHubToken and /Projects/RemoveGitHubToken, and #token-status.
    [Binding]
    public class PrivateGitHubStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public PrivateGitHubStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        // ---------------------------------------------------------- Given

        [Given("the private GitHub repository {string} accepts the token {string} and has the issue {string}")]
        public void GivenThePrivateRepository(string repository, string token, string title)
        {
            _github.PrivateRepositories[repository] = token;
            _github.Repository(repository).Add(new FakeIssue
            {
                Number = 1,
                Title = title,
                Labels = ["user-story"],
                Body = FakeGitHub.TemplateBody(title)
            });
        }

        // ---------------------------------------------------------- When

        [Given("I add the GitHub token {string} to {string}")]
        [When("I add the GitHub token {string} to {string}")]
        public async Task WhenIAddTheGitHubToken(string token, string projectName)
        {
            var projectId = await OpenProjectAsync(projectName);
            await _web.PostFormAsync("/Projects/SetGitHubToken", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["token"] = token
            });
        }

        [When("I remove the GitHub token of {string}")]
        public async Task WhenIRemoveTheGitHubToken(string projectName)
        {
            var projectId = await OpenProjectAsync(projectName);
            await _web.PostFormAsync("/Projects/RemoveGitHubToken", new Dictionary<string, string> { ["projectId"] = projectId.ToString() });
        }

        [When("the token is revoked on GitHub")]
        public void WhenTheTokenIsRevoked()
        {
            foreach (var repository in _github.PrivateRepositories.Keys.ToList())
            {
                _github.PrivateRepositories[repository] = "a-new-token-nobody-has";
            }
        }

        // ---------------------------------------------------------- Then

        [Then("I see {string} on the page of {string}")]
        public async Task ThenISeeOnThePageOf(string text, string projectName)
        {
            await OpenProjectAsync(projectName);
            Assert.StartsWith(text, _web.Page!.QuerySelector("#token-status")!.TextContent.Trim());
        }

        // R3: not in a field, an attribute or anywhere else in the HTML
        [Then("the page does not contain {string}")]
        public void ThenThePageDoesNotContain(string text)
            => Assert.DoesNotContain(text, _web.Page!.DocumentElement.OuterHtml);

        [Then("the database does not contain {string} in plain text")]
        public async Task ThenTheDatabaseDoesNotContain(string token)
        {
            await using var db = _web.CreateDbContext();
            var stored = await db.Projects.Select(p => p.GitHub!.EncryptedToken).SingleAsync();

            Assert.NotNull(stored);
            Assert.DoesNotContain(token, stored);
        }

        [Then("{string} has no GitHub token")]
        public async Task ThenHasNoGitHubToken(string projectName)
        {
            await OpenProjectAsync(projectName);
            Assert.StartsWith("No token", _web.Page!.QuerySelector("#token-status")!.TextContent.Trim());

            await using var db = _web.CreateDbContext();
            Assert.Null(await db.Projects.Where(p => p.Name == projectName).Select(p => p.GitHub!.EncryptedToken).SingleAsync());
        }

        [Then("no activity log entry contains {string}")]
        public async Task ThenNoActivityLogEntryContains(string text)
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.ActivityLog.AnyAsync(e => e.Details != null && e.Details.Contains(text)));
        }

        // ---------------------------------------------------------- Helpers

        private async Task<Guid> OpenProjectAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}");
            return id;
        }
    }
}
