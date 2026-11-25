using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-210: the management review (/Isms/ManagementReview). UI contract: Views/Isms/ManagementReview.cshtml (#review-due,
    // form#plan-review, #review-status, #input-pack sections with data-input and li lines, form#hold-review, #review-decisions
    // rows with data-decision, data-owner and data-open, form#review-decision, form#lock-review, #management-review-read-only).
    [Binding]
    public class ManagementReviewStepDefinitions
    {
        private const string Path = "/Isms/ManagementReview";
        private readonly WebScenario _web;

        public ManagementReviewStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I plan a management review on {string} with {string}")]
        [When("I plan a management review on {string} with {string}")]
        public async Task WhenIPlan(string on, string participant)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/PlanReview", new Dictionary<string, string> { ["plannedOn"] = on, ["participants"] = participant });
            AssertNoError();
        }

        [Given("a management review was held on {string}")]
        public async Task GivenHeld(string on)
        {
            await WhenIPlan(on, "Anna");
            await _web.PostFormAsync("/Isms/HoldReview", new Dictionary<string, string> { ["reviewId"] = (await LatestAsync()).ToString(), ["heldOn"] = on });
            AssertNoError();
        }

        [Given("I record the decision {string} with an action for {string} before {string}")]
        [When("I record the decision {string} with an action for {string} before {string}")]
        public async Task WhenIDecide(string text, string owner, string due)
        {
            await _web.OpenAsync(Path);
            Guid ownerId;
            await using (var db = _web.CreateDbContext())
            {
                ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == owner).Select(u => u.Id).FirstAsync();
            }

            await _web.PostFormAsync("/Isms/ReviewDecision", new Dictionary<string, string>
            {
                ["reviewId"] = (await HeldAsync()).ToString(),
                ["text"] = text,
                ["actionOwnerId"] = ownerId.ToString(),
                ["actionDueOn"] = due
            });
            AssertNoError();
        }

        [When("I lock the minutes")]
        public async Task WhenILock()
        {
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector("form#lock-review"));
            await _web.PostFormAsync("/Isms/LockReview", new Dictionary<string, string> { ["reviewId"] = (await HeldAsync()).ToString() });
            AssertNoError();
        }

        [When("I open the management review")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("the input pack has the parts {string}, {string}, {string}, {string}, {string}, {string}")]
        public async Task ThenParts(string a, string b, string c, string d, string e, string f)
        {
            await _web.OpenAsync(Path);
            var parts = _web.Page!.QuerySelectorAll("#input-pack [data-input]").Select(s => s.GetAttribute("data-input")).ToList();
            Assert.All(new[] { a, b, c, d, e, f }, part => Assert.Contains(part, parts));
        }

        [Then("the input {string} says {string}")]
        public async Task ThenInputSays(string input, string line)
        {
            await _web.OpenAsync(Path);
            Assert.Contains(line, Lines(input));
        }

        [Then("the input {string} of the held review lists {string} but not {string}")]
        public async Task ThenKept(string input, string present, string absent)
        {
            await _web.OpenAsync(Path);
            var lines = Lines(input);
            Assert.Contains(present, lines);
            Assert.DoesNotContain(lines, l => l.Contains(absent, StringComparison.Ordinal));
        }

        [Then("the minutes cannot be changed")]
        public async Task ThenLocked()
        {
            await _web.OpenAsync(Path);
            Assert.Equal("Locked", _web.Page!.QuerySelector("#review-status")?.TextContent.Trim());
            Assert.Null(_web.Page.QuerySelector("form#review-decision, form#lock-review, form#hold-review, form#change-review"));
            await _web.PostFormAsync("/Isms/ReviewDecision", new Dictionary<string, string> { ["reviewId"] = (await HeldAsync()).ToString(), ["text"] = "Later" });
            Assert.Contains("The minutes are locked", _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? "", StringComparison.Ordinal);
        }

        [Then("the action {string} is open for {string}")]
        public async Task ThenOpenAction(string decision, string owner)
        {
            await _web.OpenAsync(Path);
            var row = _web.Page!.QuerySelector($"#review-decisions tr[data-decision='{decision}']") ?? throw new Xunit.Sdk.XunitException($"No decision '{decision}'.");
            Assert.Equal(owner, row.GetAttribute("data-owner"));
            Assert.Equal("true", row.GetAttribute("data-open"));
            Assert.NotNull(row.QuerySelector("form.review-action-done"));
        }

        [Then("the management review is due")]
        public void ThenDue() => Assert.NotNull(_web.Page!.QuerySelector("#review-due"));

        [Then("the management review is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#management-review-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#plan-review, form#hold-review, form#review-decision, form#lock-review, form.review-action-done"));
        }

        private List<string> Lines(string input)
            => (_web.Page!.QuerySelector($"#input-pack [data-input='{input}']") ?? throw new Xunit.Sdk.XunitException($"No input '{input}'."))
                .QuerySelectorAll("li").Select(l => l.TextContent.Trim()).ToList();

        private async Task<Guid> LatestAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.ManagementReviews.OrderByDescending(r => r.CreatedAt).Select(r => r.Id).FirstAsync();
        }

        private async Task<Guid> HeldAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.ManagementReviews.Where(r => r.HeldOn != null).OrderByDescending(r => r.HeldOn).Select(r => r.Id).FirstAsync();
        }

        private void AssertNoError() => Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
    }
}
