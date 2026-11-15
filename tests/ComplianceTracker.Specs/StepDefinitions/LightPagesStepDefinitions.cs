using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-154: the size of every page and tab with realistic data, and the long pickers on each
    [Binding]
    public class LightPagesStepDefinitions
    {
        // A picker with this many options is "long"; a page may have a few (e.g. one "add" form), not one per row
        private const int LongPicker = 20;
        private const int LongPickersPerPage = 3;

        private readonly WebScenario _web;
        private readonly Dictionary<string, (int Bytes, int LongPickers)> _pages = [];

        public LightPagesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open every page and tab of {string}")]
        public async Task WhenIOpenEveryPage(string project)
        {
            Guid projectId, riskId, evidenceId;
            await using (var db = _web.CreateDbContext())
            {
                projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
                riskId = await db.Risks.Where(r => r.ProjectId == projectId).Select(r => r.Id).FirstAsync();
                evidenceId = await db.Evidence.Where(e => e.ProjectId == projectId).Select(e => e.Id).FirstAsync();
            }

            var paths = new List<string>
            {
                "/Projects", "/Frameworks", "/Frameworks/BuiltIn", "/Organisation", "/Documents", "/Users", "/Activity", "/Settings",
                $"/Risks/Details/{riskId}", "/Projects/Create",
                $"/Projects/Details/{projectId}/compliance/cra/I.2(d)",
                $"/Projects/Details/{projectId}/evidence/{evidenceId}"
            };
            foreach (var tab in new[] { "overview", "frameworks", "compliance", "evidence", "risks", "security-events", "scans", "product-requirements", "suggestions", "activity", "reports" })
            {
                paths.Add($"/Projects/Details/{projectId}/{tab}");
            }

            foreach (var path in paths)
            {
                await _web.OpenAsync(path);
                Assert.True(_web.Response!.IsSuccessStatusCode, $"{path} answered {(int)_web.Response.StatusCode}");
                var bytes = (await _web.Response.Content.ReadAsByteArrayAsync()).Length;
                var pickers = _web.Page!.QuerySelectorAll("select").Count(s => s.QuerySelectorAll("option").Length >= LongPicker);
                _pages[path] = (bytes, pickers);
            }
        }

        [Then("each page is under {int} KB")]
        public void ThenEachPageIsUnder(int kilobytes)
        {
            var heavy = _pages.Where(p => p.Value.Bytes >= kilobytes * 1024).Select(p => $"{p.Key}: {p.Value.Bytes / 1024} KB").ToList();
            Assert.True(heavy.Count == 0, $"Over {kilobytes} KB:\n" + string.Join("\n", heavy));
        }

        [Then("no page repeats a picker on its rows")]
        public void ThenNoPageRepeatsAPicker()
        {
            var repeated = _pages.Where(p => p.Value.LongPickers > LongPickersPerPage).Select(p => $"{p.Key}: {p.Value.LongPickers} long pickers").ToList();
            Assert.True(repeated.Count == 0, "Pickers repeated per row:\n" + string.Join("\n", repeated));
        }
    }
}
