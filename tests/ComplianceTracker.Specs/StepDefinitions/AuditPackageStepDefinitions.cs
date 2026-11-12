using System.IO.Compression;
using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-076: the Reports tab's form #create-audit-package, and the package as it is downloaded from the locked reports
    [Binding]
    public class AuditPackageStepDefinitions
    {
        private readonly WebScenario _web;
        private Dictionary<string, byte[]>? _files;

        public AuditPackageStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I create the audit package of {string}")]
        public Task WhenICreate(string project) => CreateAsync(project, null, null);

        [When("I create the audit package of {string} from {string} to {string}")]
        public Task WhenICreateForAPeriod(string project, string from, string to) => CreateAsync(project, from, to);

        [When("{string} creates the audit package of {string}")]
        public async Task WhenCreates(string userName, string project)
        {
            var id = await ProjectIdAsync(project);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{id}/reports"));
            var form = page.QuerySelector("form#create-audit-package") ?? throw new Xunit.Sdk.XunitException("The Reports tab has no audit package.");
            var fields = new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["__RequestVerificationToken"] = form.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!
            };
            (await client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields))).EnsureSuccessStatusCode();
        }

        [Then("the audit package contains {string}, {string}, {string} and {string}")]
        public async Task ThenContainsFour(string a, string b, string c, string d)
        {
            var files = await FilesAsync();
            Assert.All(new[] { a, b, c, d }, name => Assert.True(files.ContainsKey(name), $"The package has no '{name}'; it has {string.Join(", ", files.Keys)}."));
        }

        [Then("the audit package contains {string}")]
        public async Task ThenContains(string name)
        {
            var files = await FilesAsync();
            Assert.True(files.ContainsKey(name), $"The package has no '{name}'; it has {string.Join(", ", files.Keys)}.");
        }

        [Then("the audit package's contents give {string} the SHA-256 of the evidence {string}")]
        public async Task ThenTheContentsGiveTheSha(string path, string evidence)
        {
            await using var db = _web.CreateDbContext();
            var sha = await db.Evidence.Where(e => e.Title == evidence).Select(e => e.Sha256).SingleAsync();
            var lines = (await ContentsAsync()).Split('\n').Select(l => l.Trim()).ToList();
            Assert.Equal($"SHA-256 {sha}", lines[lines.IndexOf(path) + 2]);
        }

        [Then("the audit package's contents say {string}")]
        public async Task ThenTheContentsSay(string text) => Assert.Contains(text, await ContentsAsync(), StringComparison.Ordinal);

        [Then("the activity log has {string} with the package's fingerprint")]
        public async Task ThenTheLogHasTheFingerprint(string action)
        {
            await using var db = _web.CreateDbContext();
            var sha = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").Select(s => s.Sha256).SingleAsync();
            var expected = Enum.Parse<Domain.ActivityLog.ActivityAction>(action);
            Assert.Contains(await db.ActivityLog.Where(e => e.Action == expected).Select(e => e.Details).ToListAsync(), d => d!.EndsWith($"SHA-256 {sha}", StringComparison.Ordinal));
        }

        [Then("the project {string} has {int} audit package")]
        public async Task ThenHasPackages(string project, int count)
        {
            await using var db = _web.CreateDbContext();
            var id = await ProjectIdAsync(project);
            Assert.Equal(count, await db.ReportSnapshots.CountAsync(s => s.ProjectId == id && s.Kind == "audit-package"));
        }

        // ---------------------------------------------------------- Helpers

        private async Task CreateAsync(string project, string? from, string? to)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}/reports");
            var form = _web.Page!.QuerySelector("form#create-audit-package") ?? throw new Xunit.Sdk.XunitException("The Reports tab has no audit package.");
            var fields = new Dictionary<string, string> { ["projectId"] = id.ToString() };
            if (from is not null)
            {
                fields["from"] = from;
                fields["to"] = to!;
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            _files = null;
        }

        // The newest package, downloaded like a locked report
        private async Task<Dictionary<string, byte[]>> FilesAsync()
        {
            if (_files is not null)
            {
                return _files;
            }

            await using var db = _web.CreateDbContext();
            var snapshot = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").OrderByDescending(s => s.LockedAt).FirstAsync();
            var response = await _web.Client.GetAsync($"/Projects/LockedReport/{snapshot.ProjectId}?snapshotId={snapshot.Id}");
            response.EnsureSuccessStatusCode();
            using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
            _files = zip.Entries.ToDictionary(e => e.FullName, e =>
            {
                using var stream = e.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            });
            return _files;
        }

        private async Task<string> ContentsAsync() => Encoding.UTF8.GetString((await FilesAsync())["00-contents.txt"]);

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
