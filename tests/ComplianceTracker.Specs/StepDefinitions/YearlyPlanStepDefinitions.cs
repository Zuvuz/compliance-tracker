using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-215: the yearly plan (/Isms/YearlyPlan). UI contract: Views/Isms/YearlyPlan.cshtml (#task-suggestions forms with
    // data-suggestion, #recurring-tasks rows with data-task, data-due and data-overdue, form.complete-task,
    // form.task-nonconformity, form#add-task, #yearly-plan-read-only). The nonconformity steps of US-212 check the result.
    [Binding]
    public class YearlyPlanStepDefinitions
    {
        private const string Path = "/Isms/YearlyPlan";
        private readonly WebScenario _web;

        public YearlyPlanStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("the quarterly task {string} owned by {string} with the deadline {string} for the control {string}")]
        public async Task GivenTask(string title, string owner, string due, string control)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/AddRecurringTask", new Dictionary<string, string>
            {
                ["title"] = title,
                ["interval"] = "Quarterly",
                ["ownerId"] = (await MemberAsync(owner)).ToString(),
                ["nextDueOn"] = due,
                ["controls"] = control
            });
            AssertNoError();
        }

        [Given("I accept the suggested task {string} with the first deadline {string}")]
        [When("I accept the suggested task {string} with the first deadline {string}")]
        public async Task WhenIAccept(string title, string due)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector($"#task-suggestions form[data-suggestion='{title}']")
                       ?? throw new Xunit.Sdk.XunitException($"'{title}' is not suggested.");
            await _web.PostFormAsync("/Isms/AcceptTaskSuggestion", new Dictionary<string, string>
            {
                ["key"] = form.QuerySelector("input[name=key]")!.GetAttribute("value")!,
                ["ownerId"] = (await MemberAsync("Anna")).ToString(),
                ["nextDueOn"] = due
            });
            AssertNoError();
        }

        [When("I mark {string} as done on {string} with the evidence {string}")]
        public async Task WhenIMarkDone(string title, string on, string file)
        {
            await _web.OpenAsync(Path);
            await _web.PostFileAsync("/Isms/CompleteRecurringTask", new Dictionary<string, string>
            {
                ["taskId"] = (await TaskIdAsync(title)).ToString(),
                ["doneOn"] = on
            }, file, TestFiles.Pdf());
            AssertNoError();
        }

        [When("I mark {string} as done on {string} without evidence")]
        public async Task WhenIMarkDoneWithout(string title, string on)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/CompleteRecurringTask", new Dictionary<string, string>
            {
                ["taskId"] = (await TaskIdAsync(title)).ToString(),
                ["doneOn"] = on
            });
        }

        [When("I raise a nonconformity for the overdue task {string}")]
        public async Task WhenIRaise(string title)
        {
            await _web.OpenAsync(Path);
            Assert.Equal("true", Row(title).GetAttribute("data-overdue"));
            await _web.PostFormAsync("/Isms/RecurringTaskNonconformity", new Dictionary<string, string> { ["taskId"] = (await TaskIdAsync(title)).ToString() });
            AssertNoError();
        }

        [When("I open the yearly plan")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("the next deadline of {string} is {string}")]
        public async Task ThenNextDeadline(string title, string due)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(due, Row(title).GetAttribute("data-due"));
        }

        [Then("the plan has the task {string} due {string}")]
        public Task ThenPlanHas(string title, string due) => ThenNextDeadline(title, due);

        [Then("the evidence {string} is linked to the control {string}")]
        public async Task ThenLinked(string file, string control)
        {
            await using var db = _web.CreateDbContext();
            var document = await db.OrganisationDocuments.Include(d => d.Links).SingleAsync(d => d.FileName == file);
            Assert.Contains(document.ActiveLinks, l => l.RequirementCode == control);
        }

        [Then("the task {string} is suggested")]
        public void ThenSuggested(string title) => Assert.NotNull(_web.Page!.QuerySelector($"#task-suggestions form[data-suggestion='{title}']"));

        [Then("the task {string} is no longer suggested")]
        public async Task ThenNotSuggested(string title)
        {
            await _web.OpenAsync(Path);
            Assert.Null(_web.Page!.QuerySelector($"#task-suggestions form[data-suggestion='{title}']"));
        }

        [Then("the plan has no tasks")]
        public void ThenNoTasks() => Assert.NotNull(_web.Page!.QuerySelector("#recurring-tasks-empty"));

        [Then("the yearly plan says {string}")]
        public void ThenSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("the yearly plan is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#yearly-plan-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#add-task, form.complete-task, form.accept-suggestion, form.task-nonconformity"));
        }

        private async Task<Guid> TaskIdAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            return await db.RecurringTasks.Where(t => t.Title == title).Select(t => t.Id).SingleAsync();
        }

        private async Task<Guid> MemberAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == name).Select(u => u.Id).FirstAsync();
        }

        private AngleSharp.Dom.IElement Row(string title)
            => _web.Page!.QuerySelector($"#recurring-tasks tr[data-task='{title}']") ?? throw new Xunit.Sdk.XunitException($"No task '{title}'.");

        private void AssertNoError() => Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
    }
}
