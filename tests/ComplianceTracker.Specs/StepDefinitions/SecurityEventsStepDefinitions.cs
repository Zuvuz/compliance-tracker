using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-073: the Security events tab (Views/Projects/_SecurityEventsTab.cshtml). The scenarios give times in UTC; the
    // forms send local time, as a browser does, and the page shows local time, so the steps convert both ways.
    [Binding]
    public class SecurityEventsStepDefinitions
    {
        private const string Tab = "security-events";

        private readonly WebScenario _web;

        public SecurityEventsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I have recorded the actively exploited vulnerability {string} for {string}, aware since {string}")]
        [When("I record the actively exploited vulnerability {string} for {string}, aware since {string}")]
        public Task WhenIRecordAVulnerability(string title, string project, string awareAt)
            => RecordAsync(project, "ActivelyExploitedVulnerability", title, awareAt);

        [Given("I have recorded the severe incident {string} for {string}, aware since {string}")]
        public Task GivenIRecordedAnIncident(string title, string project, string awareAt)
            => RecordAsync(project, "SevereIncident", title, awareAt);

        [When("I mark the {string} for {string} as sent with the reference {string}")]
        public async Task WhenIMarkSent(string report, string title, string reference)
        {
            await OpenAsync();
            var form = Report(title, report).QuerySelector("form.mark-report-sent")
                ?? throw new Xunit.Sdk.XunitException($"The {report} for {title} cannot be marked as sent.");
            var fields = HiddenFields(form);
            fields["sentAt"] = form.QuerySelector("input[name=sentAt]")!.GetAttribute("value")!;
            fields["reference"] = reference;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I mark the fix for {string} as available")]
        public async Task WhenIMarkTheFixAvailable(string title)
        {
            await OpenAsync();
            var form = Event(title).QuerySelector("form.mark-fix-available")!;
            var fields = HiddenFields(form);
            fields["fixAvailableAt"] = form.QuerySelector("input[name=fixAvailableAt]")!.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I close the security event {string}")]
        public async Task WhenICloseTheEvent(string title)
        {
            await OpenAsync();
            var form = Event(title).QuerySelector("form.close-security-event")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        // ---------------------------------------------------------- Then

        [Then("the {string} for {string} is due {string}")]
        public async Task ThenTheReportIsDue(string report, string title, string dueUtc)
        {
            await OpenAsync();
            Assert.Equal(Local(dueUtc).ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
                Report(title, report).QuerySelector(".report-due")!.TextContent.Trim());
        }

        [Then("the {string} for {string} is not due yet")]
        public async Task ThenTheReportIsNotDueYet(string report, string title)
        {
            await OpenAsync();
            Assert.Equal("Not due yet", Report(title, report).QuerySelector(".report-status")!.TextContent.Trim());
        }

        [Then("the {string} for {string} is sent with the reference {string}")]
        public async Task ThenTheReportIsSent(string report, string title, string reference)
        {
            await OpenAsync();
            var status = Report(title, report).QuerySelector(".report-status")!.TextContent.Trim();
            Assert.StartsWith("Sent", status);
            Assert.EndsWith($"ref. {reference}", status);
        }

        [Then("\"Needs attention\" says {string} in red")]
        public void ThenNeedsAttentionSaysInRed(string text)
        {
            var item = _web.Page!.QuerySelectorAll("#needs-attention li").FirstOrDefault(li => li.TextContent.Trim() == text)
                ?? throw new Xunit.Sdk.XunitException($"\"Needs attention\" does not say '{text}'.");
            Assert.Contains("attention-missed", item.ClassName ?? "");
        }

        // ---------------------------------------------------------- Helpers

        private async Task RecordAsync(string project, string kind, string title, string awareAtUtc)
        {
            await OpenAsync(project);
            var form = _web.Page!.QuerySelector("#record-security-event")
                ?? throw new Xunit.Sdk.XunitException("The tab has no form to record a security event.");
            var fields = HiddenFields(form);
            fields["kind"] = kind;
            fields["title"] = title;
            fields["awareAt"] = Local(awareAtUtc).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            var errors = _web.Page!.QuerySelectorAll(".validation-summary-errors li").Select(e => e.TextContent.Trim()).ToList();
            Assert.True(errors.Count == 0, string.Join(", ", errors));
        }

        private static DateTime Local(string utc) => ClockStepDefinitions.Parse(utc).UtcDateTime.ToLocalTime();

        private async Task OpenAsync(string project = "Acme Portal")
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/{Tab}");
        }

        private IElement Event(string title)
            => _web.Page!.QuerySelectorAll(".security-event").FirstOrDefault(e => e.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The tab shows no security event '{title}'.");

        private IElement Report(string title, string report)
            => Event(title).QuerySelectorAll(".security-report")
                   .FirstOrDefault(r => r.QuerySelector("td")!.TextContent.Trim() == report)
               ?? throw new Xunit.Sdk.XunitException($"'{title}' has no report '{report}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
