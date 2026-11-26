using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-227: Views/Review/Index.cshtml as Home: #my-tasks-title, #my-tasks and #my-tasks-later with li.task-item
    // (data-kind) holding .task-title, .task-due and a.task-open; #tasks-empty. Opened from the top bar's #nav-review.
    [Binding]
    public class HomeStepDefinitions
    {
        private readonly WebScenario _web;

        public HomeStepDefinitions(WebScenario web) => _web = web;

        private List<IElement> Week => _web.Page!.QuerySelectorAll("#my-tasks li.task-item").ToList();

        private List<IElement> All => _web.Page!.QuerySelectorAll("#my-tasks li.task-item, #my-tasks-later li.task-item").ToList();

        private static string Title(IElement item) => item.QuerySelector(".task-title")!.TextContent.Trim();

        [When("I open Home")]
        public async Task WhenIOpenHome()
        {
            await _web.OpenAsync("/Projects");
            var link = _web.Page!.QuerySelector("#top-bar a#nav-review") ?? throw new Xunit.Sdk.XunitException("The top bar has no \"Home\".");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("I am on Home")]
        public void ThenIAmOnHome()
        {
            Assert.Equal("/Review", _web.CurrentPath);
            Assert.Equal("Your week", _web.Page!.QuerySelector("#my-tasks-title")?.TextContent.Trim());
        }

        [Then("the first item on Home is {string} marked overdue")]
        public void ThenTheFirstIsOverdue(string title)
        {
            var first = Week.FirstOrDefault() ?? throw new Xunit.Sdk.XunitException("Home has nothing this week.");
            Assert.Equal(title, Title(first));
            Assert.StartsWith("Overdue", first.QuerySelector(".task-due")!.TextContent.Trim(), StringComparison.Ordinal);
        }

        [Then("Home lists {string} before {string}")]
        public void ThenBefore(string first, string second)
        {
            var titles = Week.Select(Title).ToList();
            Assert.Contains(first, titles);
            Assert.Contains(second, titles);
            Assert.True(titles.IndexOf(first) < titles.IndexOf(second), $"Home lists: {string.Join(" | ", titles)}");
        }

        [Then("Home lists {string} under \"Show more\"")]
        public void ThenUnderShowMore(string title)
            => Assert.Contains(title, _web.Page!.QuerySelectorAll("#my-tasks-later li.task-item").Select(Title));

        [Then("Home has nothing for this week")]
        public void ThenNothingThisWeek()
        {
            Assert.Empty(Week);
            Assert.NotNull(_web.Page!.QuerySelector("#tasks-empty"));
        }

        [Then("Home lists an approval about {string} that opens the approvals of {string}")]
        public async Task ThenAnApproval(string about, string project)
        {
            var approval = All.Where(i => i.GetAttribute("data-kind") == "Approval").SingleOrDefault(i => Title(i).Contains(about, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"Home has no approval about '{about}'. It lists: {string.Join(" | ", All.Select(Title))}");
            Assert.StartsWith("Approve: ", Title(approval), StringComparison.Ordinal);

            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.EndsWith($"/Projects/Details/{id}#approvals", approval.QuerySelector("a.task-open")!.GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
        }

        [Then("{string} has no approvals on Home, since the request waits for someone else")]
        public async Task ThenNoApprovals(string name)
        {
            _web.SignInAs(name);
            await WhenIOpenHome();
            Assert.DoesNotContain(All, i => i.GetAttribute("data-kind") == "Approval");
        }
    }
}
