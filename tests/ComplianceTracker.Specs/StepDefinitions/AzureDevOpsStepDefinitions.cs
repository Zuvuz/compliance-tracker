using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-030: user stories from Azure DevOps, through the web app and a pretend dev.azure.com (FakeAzureDevOps).
    // UI contract: Views/Projects/_ProductRequirementsTab.cshtml (#azure-devops, form #link-azure, #azure-token-status) and
    // _WorkItems.cshtml (.work-item[data-reference][data-source] with .work-item-link and .work-item-acceptance,
    // #azure-skipped .skipped-issue[data-reference], #azure-sync-error).
    [Binding]
    public class AzureDevOpsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAzureDevOps _azure;

        public AzureDevOpsStepDefinitions(WebScenario web, FakeAzureDevOps azure)
        {
            _web = web;
            _azure = azure;
        }

        [Given("the Azure DevOps project {string} accepts the token {string} and has these work items:")]
        public void GivenTheAzureProjectHasTheseItems(string address, string token, DataTable table)
        {
            var items = table.Rows.Select(row => new FakeAzureItem
            {
                Id = int.Parse(row["id"]),
                Title = row["title"],
                Type = row["type"],
                AcceptanceCriteria = row["acceptance criteria"],
                State = row["state"]
            }).ToList();
            _azure.Projects[address] = (token, items);
        }

        [Given("I have linked {string} to the Azure DevOps project {string} with the token {string}")]
        [When("I link {string} to the Azure DevOps project {string} with the token {string}")]
        public async Task WhenILink(string project, string address, string token)
        {
            await OpenAsync(project);
            var form = _web.Page!.QuerySelector("form#link-azure") ?? throw new Xunit.Sdk.XunitException("The project has no Azure DevOps form.");
            var parts = address.Split('/');
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["projectId"] = (await ProjectIdAsync(project)).ToString(),
                ["organization"] = parts[0],
                ["project"] = parts[1],
                ["areaPath"] = "",
                ["token"] = token
            });
        }

        [When("I open the Product requirements of {string}")]
        public async Task WhenIOpen(string project) => await OpenAsync(project);

        [When("Azure DevOps stops accepting the token for {string}")]
        public void WhenAzureStopsAccepting(string address) => _azure.Projects[address] = ("a-token-nobody-has", _azure.Projects[address].Items);

        [Then("the Azure work item {string} links to {string}")]
        public async Task ThenTheWorkItemLinksTo(string title, string url)
            => Assert.Equal(url, (await WorkItemAsync(title)).QuerySelector(".work-item-link")?.GetAttribute("href"));

        [Then("the Azure work item {string} has the acceptance criteria {string}")]
        public async Task ThenTheWorkItemHasCriteria(string title, string criteria)
            => Assert.Contains(criteria, (await WorkItemAsync(title)).QuerySelector(".work-item-acceptance")?.TextContent ?? "(none)", StringComparison.Ordinal);

        [Then("Azure item {int} is listed as skipped with {string}")]
        public async Task ThenItemIsSkipped(int id, string reason)
        {
            await OpenAsync("Acme Portal");
            var skipped = _web.Page!.QuerySelector($"#azure-skipped .skipped-issue[data-reference='acme/Portal#{id}']")
                ?? throw new Xunit.Sdk.XunitException($"Item {id} is not listed as skipped.");
            Assert.EndsWith("Skipped: " + reason, skipped.TextContent.Trim());
        }

        [Then("Azure item {int} is not read")]
        public async Task ThenItemIsNotRead(int id)
        {
            await OpenAsync("Acme Portal");
            Assert.Null(_web.Page!.QuerySelector($".work-item[data-reference='acme/Portal#{id}']"));
            Assert.Null(_web.Page!.QuerySelector($".skipped-issue[data-reference='acme/Portal#{id}']"));
        }

        // R6: GET, and the POST of a WIQL query, which only reads
        [Then("the tracker only sent read requests to Azure DevOps")]
        public void ThenOnlyReadRequests()
        {
            Assert.NotEmpty(_azure.Requests);
            Assert.All(_azure.Requests, r => Assert.True(r.Method == HttpMethod.Get || (r.Method == HttpMethod.Post && r.Path.EndsWith("/_apis/wit/wiql", StringComparison.Ordinal)),
                $"{r.Method} {r.Path} is not a read"));
        }

        [Then("{string} is not linked to Azure DevOps")]
        public async Task ThenIsNotLinked(string project)
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.Projects.AnyAsync(p => p.Name == project && p.AzureDevOps != null));
        }

        [Then("the Azure DevOps link says {string}")]
        public void ThenTheLinkSays(string text)
            => Assert.StartsWith(text, _web.Page!.QuerySelector("#azure-token-status")?.TextContent.Trim() ?? "(no link)");

        [Then("the stored Azure DevOps token is encrypted, without {string}")]
        public async Task ThenTheTokenIsEncrypted(string token)
        {
            await using var db = _web.CreateDbContext();
            var stored = await db.Projects.Where(p => p.AzureDevOps != null).Select(p => p.AzureDevOps!.EncryptedToken).SingleAsync();
            Assert.False(string.IsNullOrEmpty(stored));
            Assert.DoesNotContain(token, stored, StringComparison.Ordinal);
        }

        [Then("the Azure sync says {string}")]
        public async Task ThenTheSyncSays(string text)
        {
            await OpenAsync("Acme Portal");
            Assert.Equal(text, _web.Page!.QuerySelector("#azure-sync-error")?.TextContent.Trim());
        }

        [Then("the work item {string} has the source {string}")]
        public async Task ThenTheWorkItemHasTheSource(string title, string source)
            => Assert.Equal(source, (await WorkItemAsync(title)).GetAttribute("data-source"));

        private async Task<IElement> WorkItemAsync(string title)
        {
            await OpenAsync("Acme Portal");
            return _web.Page!.QuerySelectorAll(".work-item").FirstOrDefault(w => w.QuerySelector(".work-item-title")?.TextContent.Trim() == title)
                ?? throw new Xunit.Sdk.XunitException($"No work item '{title}'.");
        }

        private async Task OpenAsync(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/product-requirements");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
