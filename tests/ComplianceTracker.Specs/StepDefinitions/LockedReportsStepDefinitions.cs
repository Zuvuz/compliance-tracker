using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-050: locking reports on the Reports tab, downloading them and checking fingerprints, like a user.
    // UI contract: Views/Projects/_ReportsTab.cshtml (see its header).
    [Binding]
    public class LockedReportsStepDefinitions
    {
        private readonly WebScenario _web;
        private byte[]? _downloaded;

        public LockedReportsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I have locked the traceability report of {string} for {string}")]
        [When("I lock the traceability report of {string} for {string}")]
        public async Task WhenILockTheTraceabilityReport(string project, string audience)
        {
            var form = await LockFormAsync(project, "traceability");
            var fields = Hidden(form);
            fields["audience"] = audience;
            fields["sub"] = "";
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I lock the technical documentation of {string}")]
        public async Task WhenILockTheTechnicalDocumentation(string project)
        {
            var form = await LockFormAsync(project, "technical-documentation");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // US-054
        [When("I lock the risk assessment of {string}")]
        public async Task WhenILockTheRiskAssessment(string project)
        {
            var form = await LockFormAsync(project, "risk-assessment");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I check the downloaded file of that report")]
        public async Task WhenICheckTheDownloadedFile()
        {
            await DownloadNewestAsync();
            await CheckAsync(_downloaded!);
        }

        [When("I check a changed copy of that report")]
        public async Task WhenICheckAChangedCopy()
        {
            await DownloadNewestAsync();
            var changed = _downloaded!.ToArray();
            changed[^10] ^= 0x01;
            await CheckAsync(changed);
        }

        [When("{string} opens the Reports tab of {string}")]
        public async Task WhenOpensTheReportsTab(string userName, string project)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync($"/Projects/Details/{await ProjectIdAsync(project)}/reports");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        // ---------------------------------------------------------- Then

        [Then("the locked reports of {string} list {string} locked by {string} with a fingerprint")]
        public async Task ThenTheLockedReportsList(string project, string title, string lockedBy)
        {
            await OpenAsync(project);
            var item = _web.Page!.QuerySelectorAll("li.locked-report").FirstOrDefault(l => l.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"No locked report '{title}': {string.Join(" | ", _web.Page!.QuerySelectorAll("li.locked-report").Select(l => l.GetAttribute("data-title")))}");
            Assert.Equal(lockedBy, item.QuerySelector(".locked-by")!.TextContent.Trim());
            Assert.Matches("^SHA-256 [0-9a-f]{64}$", item.QuerySelector(".locked-report-sha256")!.TextContent.Trim());
        }

        [Then("the check says {string}")]
        public void ThenTheCheckSays(string text)
            => Assert.StartsWith(text, _web.Page!.QuerySelector("#fingerprint-result")?.TextContent.Trim() ?? "", StringComparison.Ordinal);

        [Then("{string} can download the locked report {string}")]
        public async Task ThenCanDownloadTheLockedReport(string userName, string title)
        {
            var link = _web.Page!.QuerySelectorAll("li.locked-report").First(l => l.GetAttribute("data-title") == title).QuerySelector("a.locked-report-download")!;
            using var client = _web.CreateClient(signedInAs: userName);
            var response = await client.GetAsync(link.GetAttribute("href"));
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        }

        // R5: the server refuses too
        [Then("{string} cannot lock a report of {string}")]
        public async Task ThenCannotLock(string userName, string project)
        {
            var projectId = await ProjectIdAsync(project);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{projectId}/reports"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
            var before = await CountAsync(projectId);
            await client.PostAsync("/Projects/LockReport", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["report"] = "traceability",
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(before, await CountAsync(projectId));
        }

        [Then("the database refuses to change the locked report")]
        public async Task ThenTheDatabaseRefusesToChange()
        {
            await using var db = _web.CreateDbContext();
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"ReportSnapshots\" SET \"Title\" = 'Changed'"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"ReportSnapshotFiles\" SET \"Content\" = '\\x00'::bytea"));
        }

        [Then("the database refuses to delete the locked report")]
        public async Task ThenTheDatabaseRefusesToDelete()
        {
            await using var db = _web.CreateDbContext();
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"ReportSnapshotFiles\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"ReportSnapshots\""));
            Assert.Equal(1, await db.ReportSnapshots.CountAsync());
        }

        [Then("{string} has {int} locked reports")]
        public async Task ThenHasLockedReports(string project, int count)
        {
            await OpenAsync(project);
            Assert.Equal(count, _web.Page!.QuerySelectorAll("li.locked-report").Length);
        }

        // ---------------------------------------------------------- Helpers

        private async Task<IElement> LockFormAsync(string project, string report)
        {
            await OpenAsync(project);
            return _web.Page!.QuerySelectorAll("form.lock-report").FirstOrDefault(f => f.GetAttribute("data-report") == report)
                ?? throw new Xunit.Sdk.XunitException($"The Reports tab offers no lock for '{report}'.");
        }

        private async Task DownloadNewestAsync()
        {
            var href = _web.Page!.QuerySelector("li.locked-report a.locked-report-download")?.GetAttribute("href");
            if (href is null)
            {
                await using var db = _web.CreateDbContext();
                var project = await db.ReportSnapshots.OrderByDescending(s => s.LockedAt).Select(s => s.ProjectId).FirstAsync();
                await OpenAsync(project!.Value);
                href = _web.Page!.QuerySelector("li.locked-report a.locked-report-download")!.GetAttribute("href");
            }

            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            _downloaded = await response.Content.ReadAsByteArrayAsync();
        }

        private async Task CheckAsync(byte[] file)
        {
            var form = _web.Page!.QuerySelector("#check-fingerprint")!;
            var fields = Hidden(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, "report.pdf", file);
        }

        private async Task OpenAsync(string project) => await OpenAsync(await ProjectIdAsync(project));

        private Task OpenAsync(Guid projectId) => _web.OpenAsync($"/Projects/Details/{projectId}/reports");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private async Task<int> CountAsync(Guid projectId)
        {
            await using var db = _web.CreateDbContext();
            return await db.ReportSnapshots.CountAsync(s => s.ProjectId == projectId);
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
