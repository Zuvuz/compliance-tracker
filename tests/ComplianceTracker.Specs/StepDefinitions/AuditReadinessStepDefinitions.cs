using System.IO.Compression;
using System.Text;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-178: the audit package's check. UI contract: Views/Projects/_ReportsTab.cshtml (form#check-audit-package) and
    // _AuditReadiness.cshtml (#audit-check[data-ready], .readiness-line[data-ok] with .readiness-title and a.readiness-fix).
    [Binding]
    public class AuditReadinessStepDefinitions
    {
        private readonly WebScenario _web;

        public AuditReadinessStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I check the audit package of {string}")]
        public async Task WhenICheck(string project)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/reports");
            }

            var form = _web.Page!.QuerySelector("form#check-audit-package") ?? throw new Xunit.Sdk.XunitException("The Reports tab has no package check.");
            var query = string.Join("&", form.QuerySelectorAll("input[type=hidden]").Select(i => $"{i.GetAttribute("name")}={Uri.EscapeDataString(i.GetAttribute("value") ?? "")}"));
            await _web.OpenAsync($"{form.GetAttribute("action")!.Split('#')[0]}?{query}");
        }

        [Then("the check is not ready")]
        public void ThenNotReady() => Assert.Equal("false", Check().GetAttribute("data-ready"));

        [Then("the check says {string} is missing")]
        public void ThenMissing(string title) => Assert.Equal("false", Line(title).GetAttribute("data-ok"));

        [Then("the check says {string} is there")]
        public void ThenThere(string title) => Assert.Equal("true", Line(title).GetAttribute("data-ok"));

        [Then("the missing {string} links to the {string} tab")]
        public void ThenLinksTo(string title, string tab)
            => Assert.EndsWith("/" + tab, Line(title).QuerySelector("a.readiness-fix")?.GetAttribute("href") ?? "");

        [Then("the package's check says {string}")]
        public async Task ThenThePackagesCheckSays(string text)
        {
            await using var db = _web.CreateDbContext();
            var snapshot = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").OrderByDescending(s => s.LockedAt).FirstAsync();
            var response = await _web.Client.GetAsync($"/Projects/LockedReport/{snapshot.ProjectId}?snapshotId={snapshot.Id}");
            response.EnsureSuccessStatusCode();
            using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("00-check.txt")!.Open(), Encoding.UTF8);
            Assert.Contains(text, await reader.ReadToEndAsync(), StringComparison.Ordinal);
        }

        private IElement Check() => _web.Page!.QuerySelector("#audit-check") ?? throw new Xunit.Sdk.XunitException("The page shows no package check.");

        private IElement Line(string title)
            => Check().QuerySelectorAll(".readiness-line").FirstOrDefault(l => l.QuerySelector(".readiness-title")?.TextContent.Trim() == title)
               ?? throw new Xunit.Sdk.XunitException($"The check has no line '{title}'.");
    }
}
