using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-101: the Activity tab (Views/Projects/_ActivityTab.cshtml: #activity-filters, rows .activity-row,
    // #activity-count, a#activity-next). Log entries are written straight into the database to set up a scenario.
    [Binding]
    public class ActivitySearchStepDefinitions
    {
        private readonly WebScenario _web;

        public ActivitySearchStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("the activity log of {string} has an entry with the details {string}")]
        public Task GivenAnEntryWithTheDetails(string project, string details)
            => AddAsync(project, ActivityAction.EvidenceAdded, DateTime.UtcNow, details, byAnna: true);

        // At noon local time, so the day is the same in every time zone the tests run in
        [Given("{string} has evidence entries on {string}, {string} and {string}")]
        public async Task GivenEvidenceEntriesOn(string project, string first, string second, string third)
        {
            foreach (var day in new[] { first, second, third })
            {
                var local = DateOnly.ParseExact(day, "dd-MM-yyyy", CultureInfo.InvariantCulture).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local);
                await AddAsync(project, ActivityAction.EvidenceAdded, local.ToUniversalTime(), $"Evidence of {day}", byAnna: true);
            }
        }

        [Given("the system has written an entry about {string}")]
        public Task GivenTheSystemHasWrittenAnEntry(string project)
            => AddAsync(project, ActivityAction.WorkItemsSynced, DateTime.UtcNow, "Scheduled: 1 added", byAnna: false);

        // In all, counting the entries the project already has (ProjectCreated)
        [Given("the activity log of {string} has {int} entries")]
        public async Task GivenTheLogHasEntries(string project, int count)
        {
            var (organizationId, projectId, userId) = await IdsAsync(project);
            await using var db = _web.CreateDbContext();
            var existing = await db.ActivityLog.CountAsync(e => e.SubjectId == projectId);
            var start = DateTime.UtcNow.AddDays(-1);
            for (var i = 0; i < count - existing; i++)
            {
                db.ActivityLog.Add(new ActivityLogEntry(organizationId, userId, ActivityAction.RiskUpdated, start.AddMinutes(i), projectId, $"Change {i + 1}"));
            }

            await db.SaveChangesAsync();
        }

        // ---------------------------------------------------------- When

        [When("I search the activity of {string} for {string}")]
        public async Task WhenISearch(string project, string text)
            => await OpenAsync(project, "?q=" + Uri.EscapeDataString(text));

        [When("I filter the activity of {string} from {string} to {string} in the group {string}")]
        public async Task WhenIFilter(string project, string from, string to, string group)
            => await OpenAsync(project, $"?from={Iso(from)}&to={Iso(to)}&group={GroupSlug(group)}");

        [When("I filter the activity of {string} on the user {string}")]
        public async Task WhenIFilterOnTheUser(string project, string user)
            => await OpenAsync(project, "?user=" + user.ToLowerInvariant());

        [When("I sort the activity of {string} by {string}")]
        public async Task WhenISort(string project, string sort)
            => await OpenAsync(project, "?sort=" + Web.Models.ActivityFilters.Sorts.Single(s => s.Title == sort).Slug);

        [When("I open the activity of {string}")]
        public Task WhenIOpenTheActivity(string project)
            => OpenAsync(project, "");

        [When("I open the activity of {string} with {string}")]
        public Task WhenIOpenTheActivityWith(string project, string query)
            => OpenAsync(project, query);

        [When("{string} searches the activity of {string} for {string}")]
        public async Task WhenOtherSearches(string user, string project, string text)
        {
            var (_, projectId, _) = await IdsAsync(project);
            using var client = _web.CreateClient(signedInAs: user);
            _web.Response = await client.GetAsync($"/Projects/Details/{projectId}/activity?q={Uri.EscapeDataString(text)}");
        }

        // ---------------------------------------------------------- Then

        [Then("I see {int} entry")]
        [Then("I see {int} entries")]
        public void ThenISeeEntries(int count)
            => Assert.Equal(count, _web.Page!.QuerySelectorAll("#project-activity .activity-row").Length);

        [Then("it has the details {string}")]
        public void ThenItHasTheDetails(string details)
            => Assert.Equal(details, _web.Page!.QuerySelector(".activity-row .activity-details")?.TextContent.Trim());

        [Then("the first entry is {string}")]
        public void ThenTheFirstEntryIs(string action)
            => Assert.Equal(action, _web.Page!.QuerySelector(".activity-row .activity-action")?.TextContent.Trim());

        [Then("there is a link to the next page")]
        public void ThenThereIsALinkToTheNextPage()
            => Assert.Contains("page=2", _web.Page!.QuerySelector("a#activity-next")?.GetAttribute("href") ?? "");

        [Then("the search field contains {string}")]
        public void ThenTheSearchFieldContains(string text)
            => Assert.Equal(text, _web.Page!.QuerySelector<IHtmlInputElement>("#activity-filters input[name=q]")?.Value);

        [Then("the group {string} is chosen")]
        public void ThenTheGroupIsChosen(string group)
            => Assert.Equal(group, _web.Page!.QuerySelector<IHtmlSelectElement>("#activity-filters select[name=group]")?.SelectedOptions.FirstOrDefault()?.TextContent.Trim());

        // A new action without a group throws in ActivityGroups.Of
        [Then("every action of the activity log belongs to an action group")]
        public void ThenEveryActionBelongsToAGroup()
            => Assert.All(Enum.GetValues<ActivityAction>(), action => Assert.True(Enum.IsDefined(ActivityGroups.Of(action))));

        // ---------------------------------------------------------- Helpers

        private async Task OpenAsync(string project, string query)
        {
            var (_, projectId, _) = await IdsAsync(project);
            await _web.OpenAsync($"/Projects/Details/{projectId}/activity{query}");
        }

        private async Task AddAsync(string project, ActivityAction action, DateTime at, string details, bool byAnna)
        {
            var (organizationId, projectId, userId) = await IdsAsync(project);
            await using var db = _web.CreateDbContext();
            db.ActivityLog.Add(new ActivityLogEntry(organizationId, byAnna ? userId : null, action, at, projectId, details));
            await db.SaveChangesAsync();
        }

        private async Task<(Guid OrganizationId, Guid ProjectId, Guid UserId)> IdsAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var found = await db.Projects.Where(p => p.Name == project).Select(p => new { p.Id, p.OrganizationId }).SingleAsync();
            var organization = await db.Organizations.SingleAsync(o => o.Id == found.OrganizationId);
            return (found.OrganizationId, found.Id, organization.Users.First().Id);
        }

        private static string Iso(string day) => DateOnly.ParseExact(day, "dd-MM-yyyy", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string GroupSlug(string title)
            => Web.Models.ActivityFilters.Slug(Enum.GetValues<ActivityGroup>().Single(g => ActivityGroups.Title(g) == title));
    }
}
