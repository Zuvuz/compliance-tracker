using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-128: suggested security events. Findings come in through the Scans tab (US-078); the lists are the pretend ones
    // (FakeExploitedLists), fetched as the daily job does. UI contract: Views/Projects/_SecurityEventsTab.cshtml
    // (#security-event-suggestions, .event-suggestion[data-key] with .suggestion-text, forms .record-suggestion and
    // .dismiss-suggestion; #exploited-lists .exploited-list[data-name]).
    [Binding]
    public class SuggestedSecurityEventsStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly FakeExploitedLists _lists;

        public SuggestedSecurityEventsStepDefinitions(WebScenario web, FakeExploitedLists lists)
        {
            _web = web;
            _lists = lists;
        }

        [Given("the exploited-vulnerability list {string} is switched on")]
        public void GivenTheListIsSwitchedOn(string key) => _lists.SwitchedOn.Add(key);

        [Given("the exploited-vulnerability list {string} is switched off")]
        public void GivenTheListIsSwitchedOff(string key) => _lists.SwitchedOn.Remove(key);

        [Given("{string} has an open high finding {string} from {string}")]
        public Task GivenAnOpenHighFinding(string project, string cve, string tool)
            => UploadAsync(project, TestScans.Sarif(tool, new TestScans.Result(cve, 7.5)));

        [When("{string} gets a critical finding {string} from {string}")]
        public Task WhenACriticalFinding(string project, string cve, string tool)
            => UploadAsync(project, TestScans.Sarif(tool, new TestScans.Result(cve, 9.8)));

        [Given("{string} is added to the list {string}")]
        [When("{string} is added to the list {string}")]
        public Task WhenAddedToTheList(string cve, string key)
        {
            _lists.Cves[key].Add(cve);
            return RefreshAsync();
        }

        [When("the lists are fetched again")]
        public Task WhenFetchedAgain() => RefreshAsync();

        [When("the lists cannot be fetched")]
        public Task WhenTheListsCannotBeFetched()
        {
            _lists.FailWith = System.Net.HttpStatusCode.ServiceUnavailable;
            return RefreshAsync();
        }

        [When("I record the suggested event {string}")]
        public async Task WhenIRecordTheSuggestedEvent(string key)
        {
            await OpenAsync();
            var form = Suggestion(key).QuerySelector("form.record-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"'{key}' cannot be recorded.");
            var fields = Hidden(form);
            fields["awareAt"] = form.QuerySelector("input[name=awareAt]")!.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I dismiss the suggestion {string} because {string}")]
        public async Task WhenIDismiss(string key, string reason)
        {
            await OpenAsync();
            var form = Suggestion(key).QuerySelector("form.dismiss-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"'{key}' cannot be dismissed.");
            var fields = Hidden(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the Security events tab suggests {string} from {string}")]
        public async Task ThenTheTabSuggests(string key, string source)
        {
            await OpenAsync();
            Assert.Contains($"· {source},", Suggestion(key).QuerySelector(".suggestion-text")!.TextContent);
        }

        [Then("the Security events tab suggests {string}")]
        public async Task ThenTheTabSuggestsText(string text)
        {
            await OpenAsync();
            Assert.Contains(text, Suggestions().Select(s => s.QuerySelector(".suggestion-text .fw-semibold")!.TextContent.Trim()));
        }

        [Then("nothing is suggested for {string}")]
        public async Task ThenNothingIsSuggested(string project)
        {
            await OpenAsync(project);
            Assert.Empty(Suggestions());
        }

        [Then("the list {string} says it was fetched {string} and failed {string}")]
        public async Task ThenTheListSays(string name, string fetchedAt, string failedAt)
        {
            await OpenAsync();
            var item = _web.Page!.QuerySelectorAll("#exploited-lists .exploited-list").FirstOrDefault(l => l.GetAttribute("data-name") == name)
                ?? throw new Xunit.Sdk.XunitException($"The tab shows no list '{name}'.");
            Assert.Equal($"{name}: fetched {Local(fetchedAt)}; the last attempt failed ({Local(failedAt)})",
                string.Join(' ', item.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        }

        [Then("the dismissal of {string} is logged with {string}")]
        public async Task ThenTheDismissalIsLogged(string key, string reason)
        {
            await using var db = _web.CreateDbContext();
            var entries = await db.ActivityLog.Where(e => e.Action == ActivityAction.SecurityEventSuggestionDismissed).ToListAsync();
            Assert.Contains(entries, e => e.Details!.Contains(key) && e.Details.EndsWith(reason));
        }

        [Then("no list was fetched")]
        public void ThenNoListWasFetched() => Assert.Equal(0, _lists.Requests);

        // ---------------------------------------------------------- Helpers

        // As the daily job (Web/Background/ScheduledExploitedListRefresh) does it
        private async Task RefreshAsync()
        {
            using var scope = _web.App.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ComplianceTracker.Application.Security.SecurityEventSuggestionService>().RefreshAsync();
        }

        private async Task UploadAsync(string project, byte[] content)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}/scans");
            var form = _web.Page!.QuerySelector("#upload-scan")!;
            var fields = Hidden(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, "scan.sarif", content);
        }

        private async Task OpenAsync(string project = Project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/security-events");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private IReadOnlyList<IElement> Suggestions()
            => _web.Page!.QuerySelectorAll("#security-event-suggestions .event-suggestion").ToList();

        private IElement Suggestion(string key)
            => Suggestions().FirstOrDefault(s => s.GetAttribute("data-key") == key)
               ?? throw new Xunit.Sdk.XunitException($"Nothing is suggested for '{key}'.");

        private static string Local(string utc) => ClockStepDefinitions.Parse(utc).UtcDateTime.ToLocalTime().ToString("dd-MM-yyyy HH:mm");

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
