using System.Text.Json;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-099: Views/Review/Index.cshtml (#task-filter with select[name=project] and select[name=kind]; #my-tasks with
    // li.task-item[data-kind][data-project] holding .task-title, .task-due and a.task-open) and the top bar's
    // #nav-review with #nav-tasks-count[data-count-url].
    [Binding]
    public class MyTasksStepDefinitions
    {
        private readonly WebScenario _web;

        public MyTasksStepDefinitions(WebScenario web) => _web = web;

        private List<IElement> Tasks => _web.Page!.QuerySelectorAll("#my-tasks li.task-item").ToList();

        private static string Title(IElement task) => task.QuerySelector(".task-title")!.TextContent.Trim();

        [When("I open My tasks from the top bar")]
        public async Task WhenIOpenMyTasks()
        {
            await _web.OpenAsync("/Projects");
            var link = _web.Page!.QuerySelector("#top-bar a#nav-review") ?? throw new Xunit.Sdk.XunitException("The top bar has no \"My tasks\".");
            Assert.StartsWith("My tasks", link.TextContent.Trim());
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("\"My tasks\" lists {string} for {string}")]
        public void ThenItListsFor(string title, string project)
        {
            var task = Tasks.SingleOrDefault(t => Title(t) == title)
                ?? throw new Xunit.Sdk.XunitException($"\"My tasks\" does not list '{title}'. It lists: {string.Join(" | ", Tasks.Select(Title))}");
            Assert.Equal(project, task.GetAttribute("data-project"));
        }

        [Then("\"My tasks\" lists only {string}")]
        public void ThenItListsOnly(string title)
            => Assert.Equal(title, Title(Assert.Single(Tasks)));

        [Then("the tasks are sorted by due date")]
        public void ThenSortedByDueDate()
        {
            // R3: the evidence expired on 20-10-2026, the risk's review was due on 01-11-2026
            Assert.Equal(["Evidence \"Pen test report\" has expired", "Review the risk \"Stolen session cookie\""], Tasks.Select(Title).ToList());
            Assert.All(Tasks, t => Assert.StartsWith("Overdue by", t.QuerySelector(".task-due")!.TextContent.Trim()));
        }

        [Then("each task links to where it is handled")]
        public async Task ThenEachTaskLinks()
        {
            var links = Tasks.ToDictionary(t => t.GetAttribute("data-kind")!, t => t.QuerySelector("a.task-open")!.GetAttribute("href")!);
            Assert.EndsWith("/evidence", links["EvidenceExpires"]);
            Assert.Contains("/risks?risk=", links["RiskReview"]);

            // R2: the risk's link opens the project's Risks tab with the risk in the side panel (US-147)
            await _web.OpenAsync(links["RiskReview"]);
            Assert.False(string.IsNullOrEmpty(_web.Page!.QuerySelector("#risk-panel")?.GetAttribute("data-open-risk")));
        }

        [Then("the top bar counts {int} tasks")]
        public async Task ThenTheTopBarCounts(int count)
        {
            await _web.OpenAsync("/Projects");
            var url = _web.Page!.QuerySelector("#nav-tasks-count")?.GetAttribute("data-count-url")
                ?? throw new Xunit.Sdk.XunitException("The top bar has no count of tasks.");
            var response = await _web.Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(count, json.RootElement.GetProperty("count").GetInt32());
        }

        [When("I show only the kind {string}")]
        public Task WhenIShowOnlyTheKind(string kind) => ChooseAsync("kind", kind);

        [When("I show only the project {string}")]
        public Task WhenIShowOnlyTheProject(string project) => ChooseAsync("project", project);

        // The filter form is a GET form: one choice at a time, the other cleared
        private async Task ChooseAsync(string name, string text)
        {
            var form = _web.Page!.QuerySelector("#task-filter") ?? throw new Xunit.Sdk.XunitException("No task filter.");
            var option = form.QuerySelectorAll($"select[name={name}] option").SingleOrDefault(o => o.TextContent.Trim() == text)
                ?? throw new Xunit.Sdk.XunitException($"The filter has no {name} '{text}'.");
            await _web.OpenAsync($"/Review?{name}={Uri.EscapeDataString(option.GetAttribute("value")!)}");
        }

        [Then("no task has a link")]
        public void ThenNoTaskHasALink()
        {
            Assert.NotEmpty(Tasks);
            Assert.All(Tasks, t =>
            {
                Assert.Null(t.QuerySelector("a.task-open"));
                Assert.NotNull(t.QuerySelector(".task-read-only"));
            });
        }
    }
}
