using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-131: the reports as PDF, downloaded from the Reports tab like a user and read back with PdfPig.
    // UI contract: Views/Projects/_ReportsTab.cshtml (#reports-traceability-download with format "pdf",
    // a#reports-technical-documentation-pdf).
    [Binding]
    public class ReportsAsPdfStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly EditProjectsStepDefinitions _projects;
        private byte[]? _file;
        private string? _fileName;
        private string? _contentType;

        public ReportsAsPdfStepDefinitions(WebScenario web, EditProjectsStepDefinitions projects)
        {
            _web = web;
            _projects = projects;
        }

        // ---------------------------------------------------------- Given / When

        [Given("the intended purpose of {string} is {string}")]
        public Task GivenTheIntendedPurposeIs(string project, string purpose) => _projects.WhenIChangeTheIntendedPurpose(project, purpose);

        [When("I open the Reports tab of {string}")]
        public async Task WhenIOpenTheReportsTab(string project) => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/reports");

        [When("I download the traceability report for {string} as PDF from the Reports tab")]
        public Task WhenIDownloadTheTraceabilityReportAsPdf(string audience) => DownloadTraceabilityAsync("Acme Portal", audience);

        [When("I download the traceability report of {string} for {string} as PDF")]
        public Task WhenIDownloadTheTraceabilityReportOfAsPdf(string project, string audience) => DownloadTraceabilityAsync(project, audience);

        [When("I download the technical documentation as PDF from the Reports tab")]
        public async Task WhenIDownloadTheTechnicalDocumentationAsPdf()
        {
            await WhenIOpenTheReportsTab("Acme Portal");
            var href = _web.Page!.QuerySelector("a#reports-technical-documentation-pdf")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException("The Reports tab has no PDF of the technical documentation.");
            await DownloadAsync(href);
        }

        // ---------------------------------------------------------- Then

        [Then("the file is a PDF named {string}")]
        public void ThenTheFileIsAPdfNamed(string pattern)
        {
            Assert.Equal("application/pdf", _contentType);
            Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$", _fileName!);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(_file!, 0, 4));
        }

        [Then("the PDF contains {string}")]
        public void ThenThePdfContains(string text) => Assert.Contains(text, string.Join(" ", Pages()), StringComparison.Ordinal);

        [Then("every page of the PDF says {string} and {string} and {string}")]
        public void ThenEveryPageSays(string pageOf, string made, string by)
        {
            var pages = Pages();
            for (var i = 0; i < pages.Count; i++)
            {
                Assert.Contains($"Page {i + 1} of {pages.Count}", pages[i], StringComparison.Ordinal);
                Assert.Contains(made, pages[i], StringComparison.Ordinal);
                Assert.Contains(by, pages[i], StringComparison.Ordinal);
            }

            Assert.Equal("Page x of y", pageOf);
        }

        [Then("the PDF has more than {int} page")]
        public void ThenThePdfHasMoreThanPages(int pages) => Assert.True(Pages().Count > pages, $"The PDF has {Pages().Count} page(s).");

        [Then("every page with a table shows the column {string}")]
        public void ThenEveryPageWithATableShowsTheColumn(string column)
        {
            // The first page starts with the title and summary; every later page holds table rows and must repeat the header
            foreach (var page in Pages().Skip(1))
            {
                Assert.Contains(column, page, StringComparison.Ordinal);
            }
        }

        [Then("the activity log has {string} saying {string}")]
        public async Task ThenTheActivityLogHasSaying(string action, string text)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<ActivityAction>(action);
            var details = await db.ActivityLog.Where(e => e.Action == expected).Select(e => e.Details).ToListAsync();
            Assert.Contains(details, d => d != null && d.Contains(text, StringComparison.Ordinal));
        }

        [Then("the traceability report can be downloaded as {string}")]
        public void ThenTheTraceabilityReportCanBeDownloadedAs(string format)
            => Assert.Contains(_web.Page!.QuerySelectorAll("#reports-traceability-download select[name=format] option"), o => o.TextContent.Trim() == format);

        [Then("the technical documentation can be downloaded as PDF")]
        public void ThenTheTechnicalDocumentationCanBeDownloadedAsPdf()
            => Assert.NotNull(_web.Page!.QuerySelector("a#reports-technical-documentation-pdf"));

        // ---------------------------------------------------------- Helpers

        private async Task DownloadTraceabilityAsync(string project, string audience)
        {
            await WhenIOpenTheReportsTab(project);
            var form = _web.Page!.QuerySelector("#reports-traceability-download")
                ?? throw new Xunit.Sdk.XunitException("The Reports tab has no traceability download.");
            Assert.Contains(form.QuerySelectorAll("select[name=format] option"), o => o.GetAttribute("value") == "pdf");
            await DownloadAsync($"{form.GetAttribute("action")}?audience={audience}&format=pdf");
        }

        private async Task DownloadAsync(string href)
        {
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            _file = await response.Content.ReadAsByteArrayAsync();
            _contentType = response.Content.Headers.ContentType?.MediaType;
            _fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        }

        private IReadOnlyList<string> Pages()
        {
            using var pdf = PdfDocument.Open(_file!);
            // Words joined by spaces, so text drawn piece by piece reads as a sentence
            return pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))).ToList();
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
