using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-070: the check of what the extraction from a framework PDF did not use, on the draft version's page.
    // UI contract (Views/Frameworks/Version.cshtml): #extraction-check with #extraction-numbers, #extraction-warning,
    // #extraction-numbering (li.possibly-missing, li.found-twice, li.not-found-from-contents), #extraction-shall
    // (li.shall-sentence) and #extraction-left-over (li.left-over), each li with data-page.
    [Binding]
    public class ExtractionCheckStepDefinitions
    {
        private readonly WebScenario _web;

        public ExtractionCheckStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // A table with the columns "page" and "line": each page of the PDF gets its lines, top to bottom
        [Given("I have added the framework {string} with a PDF of pages:")]
        [When("I add the framework {string} with a PDF of pages:")]
        public async Task WhenIAddTheFrameworkWithAPdfOfPages(string frameworkName, DataTable table)
        {
            var pages = table.Rows
                .GroupBy(r => int.Parse(r["page"], System.Globalization.CultureInfo.InvariantCulture))
                .OrderBy(g => g.Key)
                .Select(g => g.Select(r => r["line"]).ToArray())
                .ToArray();
            await _web.OpenAsync("/Frameworks");
            await _web.PostFileAsync("/Frameworks/Add", new Dictionary<string, string> { ["Name"] = frameworkName }, "framework.pdf", TestFiles.PdfWithPages(pages));
        }

        // R6: added on the draft page shown last (the upload lands on it)
        [When("I add the requirement {string} with the title {string} and the description {string}")]
        public Task WhenIAddTheRequirementWithDescription(string code, string title, string description)
            => FrameworkUi.AddRequirementAsync(_web, FrameworkUi.OpenVersionId(_web), code, title, description);

        // R6: "Then ... Then" chains reopen the page, so a requirement added by hand is taken into account
        [Then("the extraction check of {string} shows {string}")]
        public async Task ThenTheExtractionCheckShows(string frameworkName, string text)
            => Assert.Contains(text, await CheckTextAsync(frameworkName), StringComparison.Ordinal);

        [Then("the extraction check of {string} does not show {string}")]
        public async Task ThenTheExtractionCheckDoesNotShow(string frameworkName, string text)
            => Assert.DoesNotContain(text, await CheckTextAsync(frameworkName), StringComparison.Ordinal);

        [Then("the extraction check of {string} lists {string} on page {int}")]
        public async Task ThenTheExtractionCheckListsOnPage(string frameworkName, string text, int page)
        {
            await CheckTextAsync(frameworkName);
            var items = _web.Page!.QuerySelectorAll("#extraction-check li[data-page]")
                .Where(li => li.GetAttribute("data-page") == page.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Select(li => li.TextContent);
            Assert.Contains(items, i => i.Contains(text, StringComparison.Ordinal));
        }

        [Then("the extraction check of {string} shows a warning {string}")]
        public async Task ThenTheExtractionCheckShowsAWarning(string frameworkName, string text)
        {
            await CheckTextAsync(frameworkName);
            Assert.Equal(text, _web.Page!.QuerySelector("#extraction-warning")?.TextContent.Trim());
        }

        [Then("the extraction check of {string} shows no warning")]
        public async Task ThenTheExtractionCheckShowsNoWarning(string frameworkName)
        {
            await CheckTextAsync(frameworkName);
            Assert.Null(_web.Page!.QuerySelector("#extraction-warning"));
        }

        [Then("the draft of {string} version {int} lists exactly {string}")]
        public async Task ThenTheDraftListsExactly(string frameworkName, int version, string codes)
        {
            await using var db = _web.CreateDbContext();
            var versionId = await db.FrameworkVersions
                .Where(v => v.Number == version && db.Frameworks.Any(f => f.Id == v.FrameworkId && f.Name == frameworkName))
                .Select(v => v.Id).SingleAsync();
            var found = await db.Requirements.Where(r => r.FrameworkVersionId == versionId).OrderBy(r => r.Position).Select(r => r.Code).ToListAsync();
            Assert.Equal(codes.Split(", "), found);
        }

        [Then("the {string} entry contains {string}")]
        public async Task ThenTheEntryContains(string action, string text)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<ActivityAction>(action);
            var details = await db.ActivityLog.Where(e => e.Action == expected).Select(e => e.Details).ToListAsync();
            Assert.Contains(details, d => d != null && d.Contains(text, StringComparison.Ordinal));
        }

        private async Task<string> CheckTextAsync(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var versionId = await db.FrameworkVersions
                .Where(v => db.Frameworks.Any(f => f.Id == v.FrameworkId && f.Name == frameworkName))
                .OrderByDescending(v => v.Number).Select(v => v.Id).FirstAsync();
            await _web.OpenAsync($"/Frameworks/Version/{versionId}");
            var section = _web.Page!.QuerySelector("#extraction-check") ?? throw new Xunit.Sdk.XunitException("The draft has no extraction check.");
            return string.Join(" ", section.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
