using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-216: the ISMS start page (/Isms). UI contract: Views/Isms/Index.cshtml (#my-week items with data-item and
    // data-overdue, #isms-parts rows with data-part and data-state and .part-missing, #isms-guide with .guide-next,
    // form#hide-guide, form#show-guide).
    [Binding]
    public class IsmsStartStepDefinitions
    {
        private const string Path = "/Isms";
        private readonly WebScenario _web;

        public IsmsStartStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("{string} owns the quarterly task {string} with the deadline {string}")]
        public async Task GivenOwnsTask(string owner, string title, string due)
        {
            await _web.OpenAsync("/Isms/YearlyPlan");
            Guid ownerId;
            await using (var db = _web.CreateDbContext())
            {
                ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == owner).Select(u => u.Id).FirstAsync();
            }

            await _web.PostFormAsync("/Isms/AddRecurringTask", new Dictionary<string, string>
            {
                ["title"] = title,
                ["interval"] = "Quarterly",
                ["ownerId"] = ownerId.ToString(),
                ["nextDueOn"] = due
            });
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        [Given("I hide the guided start")]
        public async Task GivenIHide()
        {
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector("form#hide-guide"));
            await _web.PostFormAsync("/Isms/IsmsGuide", new Dictionary<string, string> { ["hidden"] = "true" });
        }

        [When("I open the ISMS start page")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("{string} shows {string} before {string}")]
        public void ThenOrder(string list, string first, string second)
        {
            Assert.Equal("My week", list);
            var items = _web.Page!.QuerySelectorAll("#my-week [data-item]").Select(i => i.GetAttribute("data-item")).ToList();
            Assert.Contains(first, items);
            Assert.Contains(second, items);
            Assert.True(items.IndexOf(first) < items.IndexOf(second), string.Join(" | ", items));
        }

        [Then("{string} is marked overdue")]
        public void ThenOverdue(string item)
            => Assert.Equal("true", _web.Page!.QuerySelector($"#my-week [data-item='{item}']")?.GetAttribute("data-overdue"));

        [Then("the ISMS part {string} is red with {string}")]
        public void ThenRed(string part, string missing)
        {
            var row = _web.Page!.QuerySelector($"#isms-parts tr[data-part='{part}']") ?? throw new Xunit.Sdk.XunitException($"No part '{part}'.");
            Assert.Equal("Red", row.GetAttribute("data-state"));
            Assert.StartsWith(missing, row.QuerySelector(".part-missing")?.TextContent.Trim() ?? "", StringComparison.Ordinal);
            Assert.Contains("Needs action", row.TextContent, StringComparison.Ordinal);
        }

        [Then("the guided start says {string}")]
        public void ThenGuide(string text) => Assert.Equal(text, _web.Page!.QuerySelector("#isms-guide .guide-next")?.TextContent.Trim());

        [Then("there is no guided start")]
        public void ThenNoGuide() => Assert.Null(_web.Page!.QuerySelector("#isms-guide"));

        [Then("I can show the guided start again")]
        public async Task ThenShowAgain()
        {
            Assert.NotNull(_web.Page!.QuerySelector("form#show-guide"));
            await _web.PostFormAsync("/Isms/IsmsGuide", new Dictionary<string, string> { ["hidden"] = "false" });
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector("#isms-guide"));
        }

        [Then("the ISMS start page has no buttons to change anything")]
        public void ThenNoButtons()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#isms-parts"));
            Assert.Null(_web.Page.QuerySelector("form#hide-guide, form#show-guide"));
        }
    }
}
