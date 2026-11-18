using System.IO.Compression;
using System.Text;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-184: Settings' #export-everything, form#start-closing and form#undo-closing; the notice #organisation-closing
    [Binding]
    public class OrganisationExitStepDefinitions
    {
        private readonly WebScenario _web;
        private Dictionary<string, byte[]>? _export;
        private HttpResponseMessage? _response;

        public OrganisationExitStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I export everything of {string}")]
        public async Task WhenIExport(string organisation)
        {
            var response = await _web.Client.GetAsync("/Settings/Export");
            response.EnsureSuccessStatusCode();
            using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
            _export = zip.Entries.ToDictionary(e => e.FullName, e =>
            {
                using var stream = e.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            });
        }

        [Then("the export contains {string}")]
        public void ThenTheExportContains(string path) => Assert.True(_export!.ContainsKey(path), $"No {path}: {string.Join(", ", _export.Keys.Take(30))}");

        [Then("the export's file {string} says {string}")]
        public void ThenTheFileSays(string path, string text) => Assert.Contains(text, Encoding.UTF8.GetString(_export![path]));

        [Then("the export's file {string} does not say {string}")]
        public void ThenTheFileDoesNotSay(string path, string text) => Assert.DoesNotContain(text, Encoding.UTF8.GetString(_export![path]));

        [Then("the export has a file under {string}")]
        public void ThenHasAFileUnder(string folder) => Assert.Contains(_export!.Keys, k => k.StartsWith(folder, StringComparison.Ordinal));

        [Given("I have started closing {string}")]
        public Task GivenIHaveStartedClosing(string organisation) => StartClosingAsync(organisation);

        [When("I start closing {string} typing {string}")]
        public Task WhenIStartClosing(string organisation, string typed) => StartClosingAsync(typed);

        [When("I undo closing {string}")]
        public async Task WhenIUndo(string organisation)
        {
            await _web.OpenAsync("/Settings");
            await _web.PostFormAsync(_web.Page!.QuerySelector("form#undo-closing")!.GetAttribute("action")!, new Dictionary<string, string>());
        }

        [Given("every member sees that {string} is deleted on {string}")]
        public async Task GivenEveryMemberSees(string organisation, string day)
        {
            using var carl = _web.CreateClient(signedInAs: "Carl");
            var page = await WebScenario.ParseAsync(await carl.GetAsync("/Projects"));
            Assert.Contains(day, page.QuerySelector("#organisation-closing")?.TextContent ?? "(no notice)");
        }

        [When("the closing of organisations runs")]
        public async Task WhenTheClosingRuns()
        {
            await using var scope = _web.App.Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OrganisationExitService>().CloseDueAsync();
        }

        [Then("nothing of {string} is left but its activity log and anonymous members")]
        public async Task ThenNothingIsLeft(string organisation)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Organizations.Where(o => o.Name == organisation || o.Name.StartsWith("Closed organisation")).Select(o => o.Id).FirstAsync();
            var counts = await new ComplianceTracker.Infrastructure.Persistence.OrganisationData(db).CountAsync(id);
            var left = counts.Where(c => c.Value > 0 && c.Key is not ("ActivityLog" or "Organizations" or "Users")).ToList();
            Assert.True(left.Count == 0, "Left: " + string.Join(", ", left.Select(c => $"{c.Key} {c.Value}")));
            var users = await db.Organizations.Where(o => o.Id == id).SelectMany(o => o.Users).ToListAsync();
            Assert.All(users, u => Assert.True(u.IsErased && !u.IsActive, $"{u.Name} is not anonymised"));
            Assert.True(counts["ActivityLog"] > 0);
        }

        [Then("the organisation {string} is now called {string}")]
        public async Task ThenIsNowCalled(string organisation, string prefix)
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.Organizations.AnyAsync(o => o.Name == organisation));
            Assert.True(await db.Organizations.AnyAsync(o => o.Name.StartsWith(prefix)));
        }

        [Then("{string} still has the project {string}")]
        public async Task ThenStillHasTheProject(string organisation, string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Organizations.Where(o => o.Name == organisation).Select(o => o.Id).SingleAsync();
            Assert.True(await db.Projects.AnyAsync(p => p.OrganizationId == id && p.Name == project));
        }

        [Then("the built-in {string} still has its requirements")]
        public async Task ThenTheBuiltInStillHasItsRequirements(string framework)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Frameworks.Where(f => f.Name == framework && f.OwnerOrganizationId == null).Select(f => f.Id).SingleAsync();
            Assert.True(await db.FrameworkVersions.Where(v => v.FrameworkId == id).SelectMany(v => v.Requirements).AnyAsync());
        }

        [When("{string} tries to export everything")]
        public async Task WhenTriesToExport(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            _response = await client.GetAsync("/Settings/Export");
        }

        [Then("{string} is refused")]
        public void ThenIsRefused(string userName) => Assert.NotEqual(System.Net.HttpStatusCode.OK, _response!.StatusCode);

        private async Task StartClosingAsync(string typed)
        {
            await _web.OpenAsync("/Settings");
            var form = _web.Page!.QuerySelector("form#start-closing") ?? throw new Xunit.Sdk.XunitException("Settings has no way to close the organisation.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["confirmName"] = typed });
        }
    }
}
