using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-078: the Scans tab, through the web app with test sign-in.
    // UI contract: Views/Projects/_ScansTab.cshtml (#upload-scan, #scan-imported, #open-findings and #closed-findings rows
    // .finding with data-rule, .finding-severity and .finding-state, the forms .accept-finding and .scan-as-evidence).
    [Binding]
    public class ScanImportStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;

        public ScanImportStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        [When("I upload the SARIF file {string} from {string} with {int} findings, {int} of them critical")]
        public Task WhenIUploadTheSarifFile(string fileName, string tool, int count, int critical)
            => UploadAsync(fileName, TestScans.Findings(tool, count, critical));

        [Given("{string} has a {string} scan with the findings {string} and {string}")]
        public Task GivenHasAScan(string project, string tool, string first, string second)
            => UploadAsync("scan.sarif", TestScans.Sarif(tool, new TestScans.Result(first, 7.5), new TestScans.Result(second, 7.5)));

        [When("I upload a {string} scan with only the finding {string}")]
        public Task WhenIUploadAScanWithOnly(string tool, string finding)
            => UploadAsync("scan-2.sarif", TestScans.Sarif(tool, new TestScans.Result(finding, 7.5)));

        [When("I upload the scan {string}")]
        public Task WhenIUploadTheScan(string fileName)
            => UploadAsync(fileName, Encoding.UTF8.GetBytes("<html><body>Not a scan</body></html>"));

        [When("I accept the finding {string} because {string}")]
        public async Task WhenIAcceptTheFinding(string rule, string reason)
        {
            await OpenTabAsync();
            var form = Finding("#open-findings", rule).QuerySelector("form.accept-finding")
                ?? throw new Xunit.Sdk.XunitException($"The finding '{rule}' has no Accept button.");
            var fields = HiddenFields(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I use the {string} scan as evidence for {string}")]
        public async Task WhenIUseTheScanAsEvidence(string tool, string code)
        {
            await OpenTabAsync();
            var form = _web.Page!.QuerySelectorAll("#latest-scans .latest-scan").First(r => r.GetAttribute("data-tool") == tool)
                .QuerySelector("form.scan-as-evidence")
                ?? throw new Xunit.Sdk.XunitException($"The {tool} scan cannot be used as evidence.");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim().EndsWith(" " + code, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be chosen.");
            var fields = HiddenFields(form);
            fields["requirementId"] = option.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("I see the scan result {string}")]
        public void ThenISeeTheScanResult(string summary)
            => Assert.Equal(summary, _web.Page!.QuerySelector("#scan-imported")?.TextContent.Trim());

        [Then("the finding {string} is open with the severity {string}")]
        public async Task ThenTheFindingIsOpen(string rule, string severity)
        {
            await OpenTabAsync();
            Assert.Equal(severity, Finding("#open-findings", rule).QuerySelector(".finding-severity")?.TextContent.Trim());
        }

        [Then("the finding {string} is marked {string} today")]
        public async Task ThenTheFindingIsMarkedToday(string rule, string state)
        {
            await OpenTabAsync();
            var today = _clock.GetUtcNow().ToLocalTime().ToString("dd-MM-yyyy");
            Assert.Equal($"{state} {today}", Finding("#closed-findings", rule).QuerySelector(".finding-state")?.TextContent.Trim());
        }

        [Then("the finding {string} is marked {string}")]
        public async Task ThenTheFindingIsMarked(string rule, string state)
        {
            await OpenTabAsync();
            Assert.Equal(state, Finding("#closed-findings", rule).QuerySelector(".finding-state")?.TextContent.Trim());
        }

        [Then("the row {string} has the evidence {string} with its SHA-256")]
        public void ThenTheRowHasTheEvidence(string code, string title)
        {
            var row = RequirementRowLookup.Find(_web, code)
                ?? throw new Xunit.Sdk.XunitException($"The page has no requirement '{code}'.");
            var evidence = row.QuerySelectorAll(".evidence").FirstOrDefault(e => e.GetAttribute("data-title")?.StartsWith(title, StringComparison.Ordinal) == true)
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no evidence '{title}'.");
            Assert.NotNull(evidence.QuerySelector(".evidence-sha256"));
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string fileName, byte[] content)
        {
            await OpenTabAsync();
            var form = _web.Page!.QuerySelector("#upload-scan")
                ?? throw new Xunit.Sdk.XunitException("The Scans tab has no upload form.");
            var fields = HiddenFields(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, content);
        }

        private async Task OpenTabAsync()
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/scans");
        }

        private IElement Finding(string table, string rule)
            => _web.Page!.QuerySelectorAll($"{table} tr.finding").FirstOrDefault(r => r.GetAttribute("data-rule") == rule)
               ?? throw new Xunit.Sdk.XunitException($"The Scans tab has no finding '{rule}' in {table}.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
