using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-127: security events that are not reportable, and upgrading them. Shares the tab and UI contract of US-073
    // (Views/Projects/_SecurityEventsTab.cshtml: .no-reports, .security-event-upgraded, forms .mark-disclosed,
    // .upgrade-security-event and .close-security-event with a reason).
    [Binding]
    public class SecurityEventsNotReportableStepDefinitions
    {
        private readonly WebScenario _web;

        public SecurityEventsNotReportableStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have recorded the vulnerability {string}, not exploited, for {string}, aware since {string}")]
        [When("I record the vulnerability {string}, not exploited, for {string}, aware since {string}")]
        public Task WhenIRecordAVulnerabilityNotExploited(string title, string project, string awareAt)
            => RecordAsync(project, "VulnerabilityNotExploited", title, awareAt);

        [Given("I have recorded the incident {string}, not severe, for {string}, aware since {string}")]
        public Task GivenIRecordedAnIncidentNotSevere(string title, string project, string awareAt)
            => RecordAsync(project, "IncidentNotSevere", title, awareAt);

        [When("I upgrade {string} to reportable at {string} because {string}")]
        public async Task WhenIUpgrade(string title, string reportableAt, string reason)
        {
            await OpenAsync();
            var form = Event(title).QuerySelector("form.upgrade-security-event")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be upgraded.");
            var fields = Hidden(form);
            fields["reportableAt"] = Local(reportableAt).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I mark {string} as disclosed")]
        public async Task WhenIMarkDisclosed(string title)
        {
            await OpenAsync();
            var form = Event(title).QuerySelector("form.mark-disclosed")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be marked as disclosed.");
            var fields = Hidden(form);
            fields["disclosedAt"] = form.QuerySelector("input[name=disclosedAt]")!.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I close the security event {string} without a reason")]
        public Task WhenICloseWithoutAReason(string title) => CloseAsync(title, null);

        [When("I close the security event {string} because {string}")]
        public Task WhenICloseBecause(string title, string reason) => CloseAsync(title, reason);

        [Then("the event {string} needs no reports")]
        public async Task ThenNoReports(string title)
        {
            await OpenAsync();
            Assert.NotNull(Event(title).QuerySelector(".no-reports"));
            Assert.Empty(Event(title).QuerySelectorAll(".security-report"));
        }

        [Then("the event {string} says it is reportable since {string}")]
        public async Task ThenReportableSince(string title, string at)
        {
            await OpenAsync();
            Assert.StartsWith($"Reportable since {Local(at):dd-MM-yyyy HH:mm}", Event(title).QuerySelector(".security-event-upgraded")?.TextContent.Trim());
        }

        [Then("the event {string} is closed")]
        public async Task ThenClosed(string title)
        {
            await OpenAsync();
            Assert.NotNull(Event(title).QuerySelector(".security-event-closed"));
        }

        [Then("the event {string} is closed because {string}")]
        public async Task ThenClosedBecause(string title, string reason)
        {
            await ThenClosed(title);
            Assert.Contains($"Closed because: {reason}", Event(title).TextContent);
        }

        // ---------------------------------------------------------- Helpers

        private async Task CloseAsync(string title, string? reason)
        {
            await OpenAsync();
            var form = Event(title).QuerySelector("form.close-security-event")!;
            var fields = Hidden(form);
            if (reason is not null)
            {
                fields["reason"] = reason;
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task RecordAsync(string project, string kind, string title, string awareAtUtc)
        {
            await OpenAsync(project);
            var form = _web.Page!.QuerySelector("#record-security-event")!;
            var fields = Hidden(form);
            fields["kind"] = kind;
            fields["title"] = title;
            fields["awareAt"] = Local(awareAtUtc).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            // US-135: the action waits for a second member, who approves it here; the four-eyes rules are tested in FourEyes.feature
            await FourEyes.ApproveAllAsync(_web);
        }

        private static DateTime Local(string utc) => ClockStepDefinitions.Parse(utc).UtcDateTime.ToLocalTime();

        private async Task OpenAsync(string project = "Acme Portal")
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/security-events");
        }

        private IElement Event(string title)
            => _web.Page!.QuerySelectorAll(".security-event").FirstOrDefault(e => e.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The tab shows no security event '{title}'.");

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
