using AngleSharp.Dom;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using ComplianceTracker.Web.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-018: user stories read from GitHub, through the web app with test sign-in and a pretend GitHub (FakeGitHub).
    // UI contract: the forms /Projects/LinkGitHub and /Projects/SyncNow, the section from Views/Projects/_WorkItems.cshtml
    // (#last-synced, #sync-error, .skipped-issue, .work-item with data-reference, .work-item-title, .work-item-state,
    // .work-item-rules, .work-item-examples, .work-item-gherkin, .work-item-link), /Projects/WorkItems/{id},
    // and the SignalR hub /hubs/work-items (Follow, workItemsChanged).
    [Binding]
    public sealed class GitHubUserStoriesStepDefinitions : IAsyncDisposable
    {
        private const string Repository = "Zuvuz/compliance-tracker";
        private const string NewIssueTitle = "[US-100] Brand new story";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        // The "open page" (R7): a SignalR connection like the page's own, and the messages it got
        private HubConnection? _livePage;
        private readonly TaskCompletionSource<Guid> _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GitHubUserStoriesStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        public async ValueTask DisposeAsync()
        {
            if (_livePage is not null)
            {
                await _livePage.DisposeAsync();
            }
        }

        // ---------------------------------------------------------- Given

        [Given("the GitHub repository {string} has these issues:")]
        public void GivenTheRepositoryHasTheseIssues(string repository, DataTable table)
        {
            var issues = _github.Repository(repository);
            foreach (var row in table.Rows)
            {
                issues.Add(new FakeIssue
                {
                    Number = int.Parse(row["number"]),
                    Title = row["title"],
                    Labels = row["labels"].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Body = row["follows template"] == "yes" ? FakeGitHub.TemplateBody(row["title"]) : "Just some text, no template",
                    State = row["state"]
                });
            }
        }

        [Given("{string} is linked to {string}")]
        public Task GivenIsLinkedTo(string projectName, string repository)
            => WhenILinkToTheRepository(projectName, repository);

        [Given("{string} is linked to {string} and synced")]
        public async Task GivenIsLinkedToAndSynced(string projectName, string repository)
        {
            await WhenILinkToTheRepository(projectName, repository);
            await WhenIClickSyncNow();
        }

        // R7: the project page is open in a browser and listens for changes, like wwwroot/js/work-items.js
        [Given("I have the project page open")]
        public async Task GivenIHaveTheProjectPageOpen()
        {
            var projectId = await ProjectIdAsync("Acme Portal");
            _livePage = HubFor("Anna");
            _livePage.On<Guid>(WorkItemsHub.ChangedMessage, id => _changed.TrySetResult(id));
            await _livePage.StartAsync();
            await _livePage.InvokeAsync("Follow", projectId);
        }

        // ---------------------------------------------------------- When

        [When("I link {string} to the repository {string}")]
        public async Task WhenILinkToTheRepository(string projectName, string repository)
        {
            var projectId = await OpenProjectAsync(projectName);
            await _web.PostFormAsync("/Projects/LinkGitHub", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["repository"] = repository,
                ["label"] = "user-story"
            });
        }

        [When("I click \"Sync now\"")]
        public async Task WhenIClickSyncNow()
        {
            var projectId = await OpenProjectAsync("Acme Portal");
            await _web.PostFormAsync("/Projects/SyncNow", new Dictionary<string, string> { ["projectId"] = projectId.ToString() });
        }

        [When("issue {int} is closed on GitHub")]
        public void WhenIssueIsClosedOnGitHub(int number)
            => _github.Issue(number).State = "closed";

        [When("issue {int} no longer has the label {string}")]
        public void WhenIssueNoLongerHasTheLabel(int number, string label)
            => _github.Issue(number).Labels.Remove(label);

        [When("GitHub refuses requests because of its rate limit")]
        public void WhenGitHubRefusesRequests()
            => _github.FailWith = HttpStatusCode.Forbidden;

        // R6: runs exactly what the background job runs on its timer (ScheduledWorkItemSync)
        [When("the scheduled check runs")]
        public async Task WhenTheScheduledCheckRuns()
        {
            using var scope = _web.App.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WorkItemSyncService>().SyncAllAsync();
        }

        [When("a new issue with label {string} is added on GitHub and the scheduled check runs")]
        public async Task WhenANewIssueIsAddedAndTheScheduledCheckRuns(string label)
        {
            _github.Repository(Repository).Add(new FakeIssue
            {
                Number = 100,
                Title = NewIssueTitle,
                Labels = [label],
                Body = FakeGitHub.TemplateBody(NewIssueTitle)
            });
            await WhenTheScheduledCheckRuns();
        }

        [When("{string} opens the work items of {string}")]
        public async Task WhenOpensTheWorkItemsOf(string userName, string projectName)
        {
            var projectId = await ProjectIdAsync(projectName);
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync($"/Projects/WorkItems/{projectId}");
        }

        // ---------------------------------------------------------- Then

        [Then("the project has the work item {string}")]
        public async Task ThenTheProjectHasTheWorkItem(string title)
        {
            await OpenProjectAsync("Acme Portal");
            WorkItem(title);
        }

        [Then("the work item shows its rules, examples and Gherkin")]
        public void ThenTheWorkItemShowsItsSections()
        {
            var item = WorkItem("[US-003] Create organisation via web UI");
            Assert.Contains("R1: [US-003] Create organisation via web UI works", item.QuerySelector(".work-item-rules")!.TextContent);
            Assert.Contains("Anna tries it", item.QuerySelector(".work-item-examples")!.TextContent);
            Assert.StartsWith("Feature: [US-003]", item.QuerySelector(".work-item-gherkin")!.TextContent.Trim());
        }

        [Then("the work item links to {string}")]
        public void ThenTheWorkItemLinksTo(string url)
            => Assert.Equal(url, WorkItem("[US-003] Create organisation via web UI").QuerySelector(".work-item-link")!.GetAttribute("href"));

        [Then("the page says when it was last synced")]
        public void ThenThePageSaysWhenItWasLastSynced()
        {
            var text = _web.Page!.QuerySelector("#last-synced")!.TextContent.Trim();
            Assert.StartsWith("Last synced: ", text);
            Assert.DoesNotContain("never", text);
        }

        [Then("issue {int} is listed as skipped with {string}")]
        public void ThenIssueIsListedAsSkipped(int number, string reason)
        {
            var skipped = _web.Page!.QuerySelector($".skipped-issue[data-reference='{Repository}#{number}']")
                ?? throw new Xunit.Sdk.XunitException($"Issue {number} is not listed as skipped.");
            Assert.EndsWith("Skipped: " + reason, skipped.TextContent.Trim());
        }

        [Then("issue {int} is not read")]
        public void ThenIssueIsNotRead(int number)
        {
            var reference = $"{Repository}#{number}";
            Assert.Null(_web.Page!.QuerySelector($".work-item[data-reference='{reference}']"));
            Assert.Null(_web.Page!.QuerySelector($".skipped-issue[data-reference='{reference}']"));
        }

        // R2: GET only, never POST, PATCH or DELETE
        [Then("the tracker only sent read requests to GitHub")]
        public void ThenTheTrackerOnlySentReadRequests()
        {
            Assert.NotEmpty(_github.Requests);
            Assert.All(_github.Requests, method => Assert.Equal(HttpMethod.Get, method));
        }

        [Then("the work item {string} is marked {string}")]
        public async Task ThenTheWorkItemIsMarked(string title, string state)
        {
            await OpenProjectAsync("Acme Portal");
            Assert.Equal(state, WorkItem(title).QuerySelector(".work-item-state")!.TextContent.Trim());
        }

        // R7: the page got the message and, like work-items.js, fetches the section again
        [Then("the new work item appears on the page without reloading")]
        public async Task ThenTheNewWorkItemAppears()
        {
            var projectId = await ProjectIdAsync("Acme Portal");
            var notified = await _changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(projectId, notified);

            var section = await WebScenario.ParseAsync(await _web.Client.GetAsync($"/Projects/WorkItems/{projectId}"));
            Assert.Contains(NewIssueTitle, section.QuerySelectorAll(".work-item-title").Select(t => t.TextContent.Trim()));
        }

        // R9: the hub refuses to send another organisation's changes
        [Then("{string} cannot follow the live updates of {string}")]
        public async Task ThenCannotFollowTheLiveUpdates(string userName, string projectName)
        {
            var projectId = await ProjectIdAsync(projectName);
            await using var connection = HubFor(userName);
            await connection.StartAsync();

            var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Follow", projectId));
            Assert.Contains("Project not found", error.Message);
        }

        [Then("the activity log for {string} has a {string} entry by the system")]
        public async Task ThenTheActivityLogHasAnEntryByTheSystem(string organisationName, string action)
        {
            await using var db = _web.CreateDbContext();
            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var expected = Enum.Parse<ActivityAction>(action);

            var details = await db.ActivityLog
                .Where(e => e.OrganizationId == organisation.Id && e.Action == expected && e.UserId == null)
                .Select(e => e.Details)
                .ToListAsync();

            Assert.Contains(details, d => d != null && d.StartsWith("Scheduled: ") && d.Contains("1 closed"));
        }

        // ---------------------------------------------------------- Helpers

        // A SignalR connection through the in-memory test server, signed in as the user (see TestAuthHandler)
        private HubConnection HubFor(string userName)
        {
            var server = _web.App.Factory.Server;
            return new HubConnectionBuilder()
                .WithUrl(new Uri(server.BaseAddress, WorkItemsHub.Path), options =>
                {
                    options.Transports = HttpTransportType.LongPolling; // the test server has no real sockets
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Headers[TestAuthHandler.UserHeader] = userName;
                })
                .Build();
        }

        private IElement WorkItem(string title)
            => _web.Page!.QuerySelectorAll(".work-item")
                   .FirstOrDefault(i => i.QuerySelector(".work-item-title")?.TextContent.Trim() == title)
               ?? throw new Xunit.Sdk.XunitException($"The project page shows no work item '{title}'.");

        private async Task<Guid> OpenProjectAsync(string projectName)
        {
            var id = await ProjectIdAsync(projectName);
            await _web.OpenAsync($"/Projects/Details/{id}");
            return id;
        }

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
