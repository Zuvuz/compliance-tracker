using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-212: the register of nonconformities (/Isms/Nonconformities). UI contract: Views/Isms/Nonconformities.cshtml
    // (#nonconformities items with data-nonconformity, data-status and data-source, li.failed-action; form#raise-nonconformity
    // with the select #raise-origin, form.analyse-nonconformity, form.plan-action, form.action-done, form.check-nonconformity,
    // form.close-nonconformity, #nonconformities-read-only). The steps act on the nonconformity named last.
    [Binding]
    public class NonconformityStepDefinitions
    {
        private const string Path = "/Isms/Nonconformities";
        private readonly WebScenario _web;
        private string? _current;

        public NonconformityStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("the nonconformity {string} from an internal audit")]
        public async Task GivenTheNonconformity(string title)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/RaiseNonconformity", new Dictionary<string, string>
            {
                ["title"] = title,
                ["source"] = "InternalAudit",
                ["severity"] = "Major"
            });
            AssertNoError();
            _current = title;
        }

        [When("I raise a nonconformity from {string}")]
        public async Task WhenIRaiseFrom(string title)
        {
            await _web.OpenAsync(Path);
            var option = _web.Page!.QuerySelectorAll("#raise-origin option").FirstOrDefault(o => o.TextContent.Contains(title, StringComparison.Ordinal))
                         ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be chosen as the source.");
            await _web.PostFormAsync("/Isms/RaiseNonconformity", new Dictionary<string, string>
            {
                ["origin"] = option.GetAttribute("value")!,
                ["source"] = "Other",
                ["severity"] = "Minor"
            });
            AssertNoError();
            _current = title;
        }

        [Given("I record the cause {string}")]
        [When("I record the cause {string}")]
        public async Task WhenIRecordTheCause(string cause)
            => await PostAsync("/Isms/AnalyseNonconformity", new() { ["cause"] = cause }, expectError: false);

        [Given("I plan the corrective action {string} for {string} before {string}")]
        [When("I plan the corrective action {string} for {string} before {string}")]
        public async Task WhenIPlanTheAction(string action, string owner, string due)
        {
            Guid ownerId;
            await using (var db = _web.CreateDbContext())
            {
                ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == owner).Select(u => u.Id).FirstAsync();
            }

            await PostAsync("/Isms/PlanCorrectiveAction", new() { ["action"] = action, ["ownerId"] = ownerId.ToString(), ["dueOn"] = due }, expectError: null);
        }

        [Given("the corrective action is done")]
        [When("the corrective action is done")]
        public Task WhenTheActionIsDone() => PostAsync("/Isms/CorrectiveActionDone", new(), expectError: false);

        [When("I record the effectiveness check {string} as effective")]
        public Task WhenEffective(string result) => PostAsync("/Isms/CheckNonconformity", new() { ["result"] = result, ["effective"] = "true" }, expectError: false);

        [When("I record the effectiveness check {string} as not effective")]
        public Task WhenNotEffective(string result) => PostAsync("/Isms/CheckNonconformity", new() { ["result"] = result, ["effective"] = "false" }, expectError: false);

        [When("I try to close it")]
        public Task WhenITryToClose() => PostAsync("/Isms/CloseNonconformity", new(), expectError: null);

        [When("I open the nonconformities")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        // ---------------------------------------------------------- Then

        [Then("the nonconformity {string} is closed")]
        public async Task ThenClosed(string title)
        {
            await _web.OpenAsync(Path + "?showClosed=true");
            Assert.Equal("Effective", Item(title).GetAttribute("data-status"));
        }

        [Then("the nonconformity {string} is {string}")]
        public async Task ThenTheStatusIs(string title, string status)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(status, Item(title).GetAttribute("data-status"));
        }

        [Then("the nonconformity {string} is {string} from {string}")]
        public async Task ThenTheStatusAndSource(string title, string status, string source)
        {
            await ThenTheStatusIs(title, status);
            Assert.Equal(source, Item(title).GetAttribute("data-source"));
        }

        [Then("the nonconformity {string} lists the action {string} as not working")]
        public async Task ThenFailed(string title, string action)
        {
            await _web.OpenAsync(Path);
            Assert.Contains(Item(title).QuerySelectorAll(".failed-action"), li => li.TextContent.Trim().StartsWith(action + ":", StringComparison.Ordinal));
        }

        [Then("{string} can no longer be chosen as the source of a nonconformity")]
        public async Task ThenNoLongerAnOrigin(string title)
        {
            await _web.OpenAsync(Path);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#raise-origin option"), o => o.TextContent.Contains(title, StringComparison.Ordinal));
        }

        [Then("I am told {string}")]
        public void ThenIAmTold(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("the nonconformities are read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#nonconformities-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#raise-nonconformity, form.analyse-nonconformity, form.plan-action, form.close-nonconformity"));
        }

        // ---------------------------------------------------------- Helpers

        // expectError: false checks the page has no error; null leaves it to the Then step
        private async Task PostAsync(string path, Dictionary<string, string> fields, bool? expectError)
        {
            await _web.OpenAsync(Path);
            await using (var db = _web.CreateDbContext())
            {
                fields["nonconformityId"] = (await db.Nonconformities.Where(n => n.Title == _current).Select(n => n.Id).SingleAsync()).ToString();
            }

            await _web.PostFormAsync(path, fields);
            if (expectError == false)
            {
                AssertNoError();
            }
        }

        private void AssertNoError() => Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));

        private AngleSharp.Dom.IElement Item(string title)
            => _web.Page!.QuerySelector($"#nonconformities [data-nonconformity='{title}']")
               ?? throw new Xunit.Sdk.XunitException($"No nonconformity '{title}' in the register.");
    }
}
