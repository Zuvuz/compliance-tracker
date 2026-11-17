using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-177: the "⋯" menus (details.withdraw-menu with form.withdraw-upload) next to #latest-scans .latest-scan,
    // #sboms .sbom and #test-results-uploads .test-results-upload (Views/Shared/_WithdrawMenu.cshtml).
    [Binding]
    public class WithdrawUploadsStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;

        public WithdrawUploadsStepDefinitions(WebScenario web) => _web = web;

        private async Task<Guid> ProjectIdAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
        }

        private async Task OpenAsync(string tab) => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync()}/{tab}");

        private static Dictionary<string, string> Fields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private async Task WithdrawAsync(IElement row, string reason)
        {
            var form = row.QuerySelector("details.withdraw-menu form.withdraw-upload") ?? throw new Xunit.Sdk.XunitException("No \"Withdraw\" in the menu.");
            var fields = Fields(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I withdraw the newest {string} scan because {string}")]
        public async Task WhenIWithdrawTheNewestScan(string tool, string reason)
        {
            await OpenAsync("scans");
            var row = _web.Page!.QuerySelectorAll("#latest-scans .latest-scan").SingleOrDefault(r => r.GetAttribute("data-tool") == tool)
                ?? throw new Xunit.Sdk.XunitException($"No {tool} scan is listed.");
            await WithdrawAsync(row, reason);
        }

        // R1: the page offers only the newest scan, so the older one is sent as a crafted request
        [When("I try to withdraw the oldest {string} scan because {string}")]
        public async Task WhenITryToWithdrawTheOldest(string tool, string reason)
        {
            var projectId = await ProjectIdAsync();
            Guid scanId;
            await using (var db = _web.CreateDbContext())
            {
                scanId = await db.Scans.Where(s => s.ProjectId == projectId && s.Tool == tool).OrderBy(s => s.ScannedAt).Select(s => s.Id).FirstAsync();
            }

            await OpenAsync("scans");
            await _web.PostFormAsync("/Projects/WithdrawScan", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["scanId"] = scanId.ToString(),
                ["reason"] = reason
            });
        }

        [Then("{string} has no open findings")]
        public async Task ThenNoOpenFindings(string project)
        {
            Assert.Equal(Project, project);
            await OpenAsync("scans");
            Assert.Empty(_web.Page!.QuerySelectorAll("#open-findings .finding"));
        }

        [Then("the open findings of {string} are {string} and {string}")]
        public async Task ThenTheOpenFindingsAre(string project, string first, string second)
        {
            Assert.Equal(Project, project);
            await OpenAsync("scans");
            var rules = _web.Page!.QuerySelectorAll("#open-findings .finding").Select(f => f.GetAttribute("data-rule")).OrderBy(r => r).ToList();
            Assert.Equal([first, second], rules);
        }

        [Then("the Scans tab lists no {string} scan")]
        public async Task ThenNoScanListed(string tool)
        {
            await OpenAsync("scans");
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#latest-scans .latest-scan"), r => r.GetAttribute("data-tool") == tool);
        }

        [When("I withdraw the SBOM for version {string} because {string}")]
        public async Task WhenIWithdrawTheSbom(string version, string reason)
        {
            await OpenAsync("scans");
            var row = _web.Page!.QuerySelectorAll("#sboms .sbom").SingleOrDefault(r => r.GetAttribute("data-version") == version)
                ?? throw new Xunit.Sdk.XunitException($"No SBOM for version {version}.");
            await WithdrawAsync(row, reason);
        }

        [Then("the SBOM for version {string} has {int} components")]
        public async Task ThenTheSbomHas(string version, int count)
        {
            await OpenAsync("scans");
            var row = _web.Page!.QuerySelectorAll("#sboms .sbom").Single(r => r.GetAttribute("data-version") == version);
            Assert.Contains($"{count} components", row.QuerySelector(".sbom-summary")!.TextContent);
        }

        [When("I withdraw the test results because {string}")]
        public async Task WhenIWithdrawTheTestResults(string reason)
        {
            await OpenTestResultsAsync();
            var row = _web.Page!.QuerySelectorAll("#test-results-uploads .test-results-upload").FirstOrDefault()
                ?? throw new Xunit.Sdk.XunitException("No test results can be withdrawn.");
            await WithdrawAsync(row, reason);
        }

        [When("I withdraw the newest version of {string} because {string}")]
        public async Task WhenIWithdrawTheNewestVersion(string document, string reason)
        {
            await OpenAsync("product-requirements");
            var row = _web.Page!.QuerySelectorAll(".requirement-document").SingleOrDefault(d => d.GetAttribute("data-name") == document)
                ?? throw new Xunit.Sdk.XunitException($"No specification {document}.");
            await WithdrawAsync(row, reason);
        }

        // From the Evidence tab's link to the page with the uploads
        private async Task OpenTestResultsAsync()
        {
            await OpenAsync("evidence");
            var link = _web.Page!.QuerySelector("#test-results-link") ?? throw new Xunit.Sdk.XunitException("The Evidence tab has no link to the test results.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("no test results wait to be withdrawn")]
        public async Task ThenNoTestResultsWait()
        {
            await OpenTestResultsAsync();
            Assert.Null(_web.Page!.QuerySelector("#test-results-uploads"));
        }
    }
}
