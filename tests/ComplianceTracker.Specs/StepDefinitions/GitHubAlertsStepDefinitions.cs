using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-121: fetching code scanning and Dependabot alerts from the pretend GitHub, on the Scans tab.
    // UI contract: Views/Projects/_ScansTab.cshtml (#fetch-from-github, #scan-imported .scan-line, .latest-scan with .scan-source).
    [Binding]
    public class GitHubAlertsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public GitHubAlertsStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        [Given("GitHub has the open {string} alert {int} {string} with the severity {string} for {string}")]
        public void GivenGitHubHasTheCodeScanningAlert(string tool, int number, string rule, string severity, string repository)
            => Alerts(_github.CodeScanningAlerts, repository).Add(new FakeAlert { Number = number, Tool = tool, RuleId = rule, Severity = severity, Message = $"{rule} found" });

        [Given("GitHub has the open Dependabot alert {int} {string} with the severity {string} for {string}")]
        public void GivenGitHubHasTheDependabotAlert(int number, string cve, string severity, string repository)
            => Alerts(_github.DependabotAlerts, repository).Add(new FakeAlert { Number = number, Tool = "Dependabot", RuleId = cve, Severity = severity, Message = "Prototype pollution" });

        [Given("the alert {int} of {string} is fixed in GitHub")]
        public void GivenTheAlertIsFixed(int number, string repository)
            => Alerts(_github.CodeScanningAlerts, repository).Single(a => a.Number == number).State = "fixed";

        [Given("the alert {int} of {string} is dismissed in GitHub as {string}")]
        public void GivenTheAlertIsDismissed(int number, string repository, string reason)
        {
            var alert = Alerts(_github.CodeScanningAlerts, repository).Single(a => a.Number == number);
            alert.State = "dismissed";
            alert.DismissedReason = reason;
        }

        [Given("Dependabot alerts are switched off for {string}")]
        public void GivenDependabotIsOff(string repository)
            => _github.DependabotOff.Add(repository);

        [Given("I have fetched the security alerts of {string} from GitHub")]
        [When("I fetch the security alerts of {string} from GitHub")]
        public async Task WhenIFetch(string project)
        {
            await OpenTabAsync(project);
            var form = _web.Page!.QuerySelector("#fetch-from-github")
                ?? throw new Xunit.Sdk.XunitException("The Scans tab has no \"Fetch from GitHub\" button.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I open the Scans tab of {string}")]
        public Task WhenIOpenTheScansTab(string project) => OpenTabAsync(project);

        [Then("the fetch says {string}")]
        public void ThenTheFetchSays(string line)
            => Assert.Contains(line, _web.Page!.QuerySelectorAll("#scan-imported .scan-line").Select(l => l.TextContent.Trim()));

        [Then("the latest {string} scan comes from {string}")]
        public void ThenTheLatestScanComesFrom(string tool, string source)
        {
            var row = _web.Page!.QuerySelectorAll("#latest-scans .latest-scan").First(r => r.GetAttribute("data-tool") == tool);
            Assert.Equal(source, row.QuerySelector(".scan-source")?.TextContent.Trim());
        }

        private static List<FakeAlert> Alerts(Dictionary<string, List<FakeAlert>> source, string repository)
        {
            if (!source.TryGetValue(repository, out var alerts))
            {
                alerts = [];
                source[repository] = alerts;
            }

            return alerts;
        }

        private async Task OpenTabAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/scans");
        }
    }
}
