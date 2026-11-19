using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-160: secret scanning alerts from the pretend GitHub, and alerts the organisation's monitoring sends to
    // POST /api/projects/{id}/alerts with the project's upload token (as a webhook does: no sign-in, no cookie).
    [Binding]
    public class SecurityAlertsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeGitHub _github;
        private readonly PipelineScanUploadStepDefinitions _pipeline;

        public SecurityAlertsStepDefinitions(WebScenario web, FakeGitHub github, PipelineScanUploadStepDefinitions pipeline)
        {
            _web = web;
            _github = github;
            _pipeline = pipeline;
        }

        [Given("GitHub has the open secret scanning alert {int} {string} named {string} for {string}")]
        public void GivenASecretScanningAlert(int number, string type, string name, string repository)
        {
            if (!_github.SecretScanningAlerts.TryGetValue(repository, out var alerts))
            {
                _github.SecretScanningAlerts[repository] = alerts = [];
            }

            alerts.Add(new FakeAlert { Number = number, Tool = "Secret scanning", RuleId = type, Severity = "critical", Message = name });
        }

        [When("the monitoring of {string} sends")]
        public async Task WhenTheMonitoringSends(string project, string json)
            => await _pipeline.SendAlertsAsync(project, Encoding.UTF8.GetBytes(json));

        [When("the monitoring of {string} sends {int} KB")]
        public async Task WhenTheMonitoringSendsKb(string project, int kilobytes)
            => await _pipeline.SendAlertsAsync(project, Encoding.UTF8.GetBytes("{\"alerts\":[]," + new string(' ', kilobytes * 1024) + "}"));

        [Then("the suggestion {string} was closed with {string}")]
        public async Task ThenTheSuggestionWasClosed(string key, string reason)
        {
            await using var db = _web.CreateDbContext();
            var suggestion = await db.SecurityEventSuggestions.SingleAsync(s => s.Key == key);
            Assert.Equal((false, reason), (suggestion.IsPending, suggestion.DismissedReason));
        }

        [Then("no activity entry contains {string}")]
        public async Task ThenNoActivityEntryContains(string text)
        {
            await using var db = _web.CreateDbContext();
            Assert.DoesNotContain(await db.ActivityLog.Select(e => e.Details).ToListAsync(), d => d != null && d.Contains(text));
        }
    }
}
