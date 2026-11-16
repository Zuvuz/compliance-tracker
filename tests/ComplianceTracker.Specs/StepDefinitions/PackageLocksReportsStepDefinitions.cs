using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-198: the reports locked with an audit package. UI contract: the Reports tab's li.locked-report of the package with
    // ul.locked-package-reports li.package-report. The management report is the one locked with the newest package.
    [Binding]
    public class PackageLocksReportsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly AuditPackageStepDefinitions _packages;

        public PackageLocksReportsStepDefinitions(WebScenario web, AuditPackageStepDefinitions packages)
        {
            _web = web;
            _packages = packages;
        }

        [Given("I have created the audit package of {string}")]
        public Task GivenIHaveCreated(string project) => _packages.WhenICreate(project);

        [Then("the newest audit package has {int} locked reports under it on the Reports tab")]
        public async Task ThenThePackageHasLockedReports(int count)
        {
            await using var db = _web.CreateDbContext();
            var package = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").OrderByDescending(s => s.LockedAt).FirstAsync();
            Assert.Equal(count, await db.ReportSnapshots.CountAsync(s => s.PackageId == package.Id));

            await _web.OpenAsync($"/Projects/Details/{package.ProjectId}/reports");
            var item = _web.Page!.QuerySelectorAll("li.locked-report").FirstOrDefault(l => l.GetAttribute("data-title") == package.Title)
                ?? throw new Xunit.Sdk.XunitException($"The package '{package.Title}' is not listed.");
            Assert.Equal(count, item.QuerySelectorAll("ul.locked-package-reports li.package-report").Length);
            // The package's reports are not listed again on their own
            Assert.Single(_web.Page.QuerySelectorAll("li.locked-report"));
        }

        [Then("the package's management report says {string}")]
        public async Task ThenTheManagementReportSays(string text)
            => Assert.Contains(text, await ManagementReportTextAsync(), StringComparison.Ordinal);

        [Then("the package's management report does not say {string}")]
        public async Task ThenTheManagementReportDoesNotSay(string text)
            => Assert.DoesNotContain(text, await ManagementReportTextAsync(), StringComparison.Ordinal);

        private async Task<string> ManagementReportTextAsync()
        {
            await using var db = _web.CreateDbContext();
            var package = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").OrderByDescending(s => s.LockedAt).FirstAsync();
            var report = await db.ReportSnapshots.SingleAsync(s => s.PackageId == package.Id && s.Kind.StartsWith("traceability:management:"));
            var response = await _web.Client.GetAsync($"/Projects/LockedReport/{report.ProjectId}?snapshotId={report.Id}");
            response.EnsureSuccessStatusCode();
            using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
            return string.Join(" ", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
        }
    }
}
