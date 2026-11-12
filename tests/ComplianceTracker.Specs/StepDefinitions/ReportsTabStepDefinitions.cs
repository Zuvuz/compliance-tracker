using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-129: the project's Reports tab. UI contract: Views/Projects/_ReportsTab.cshtml (#project-reports-tab, .report with
    // data-report and data-title, .report-for, .report-unavailable, form #reports-traceability-download,
    // a#reports-technical-documentation) and the Overview's card #project-reports with a#reports-link.
    [Binding]
    public class ReportsTabStepDefinitions
    {
        private readonly WebScenario _web;

        public ReportsTabStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the reports of {string}")]
        public async Task WhenIOpenTheReports(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/reports");

        // US-120 R8: through the tab's own form, with the reader and format the user picks
        [When("I download the traceability report {string} as {string} from the Reports tab of {string}")]
        public async Task WhenIDownloadTraceability(string audience, string format, string project)
        {
            await WhenIOpenTheReports(project);
            var form = _web.Page!.QuerySelector("#reports-traceability-download")
                ?? throw new Xunit.Sdk.XunitException("The Reports tab has no traceability download.");
            var query = $"?audience={audience}&format={format}&sub=";
            _web.Response = await _web.Client.GetAsync(form.GetAttribute("action") + query);
        }

        [Then("I see the reports {string} and {string}")]
        public void ThenISeeTheReports(string first, string second)
            => Assert.Equal([first, second], Reports().Select(r => r.GetAttribute("data-title")!));

        [Then("I see the reports {string}, {string} and {string}")]
        public void ThenISeeThreeReports(string first, string second, string third)
            => Assert.Equal([first, second, third], Reports().Select(r => r.GetAttribute("data-title")!));

        [Then("the {string} report is for {string}")]
        public void ThenTheReportIsFor(string title, string forWhom)
            => Assert.StartsWith($"For: {forWhom}", Report(title).QuerySelector(".report-for")!.TextContent.Trim());

        [Then("the {string} report cannot be made because {string}")]
        public void ThenTheReportCannotBeMade(string title, string reason)
        {
            Assert.Equal(reason, Report(title).QuerySelector(".report-unavailable")?.TextContent.Trim());
            Assert.Empty(Report(title).QuerySelectorAll("form, a"));
        }

        [Then("the {string} report can be downloaded")]
        public void ThenTheReportCanBeDownloaded(string title)
        {
            Assert.Null(Report(title).QuerySelector(".report-unavailable"));
            Assert.NotEmpty(Report(title).QuerySelectorAll("form, a"));
        }

        [Then("the Reports card links to the {string} tab")]
        public void ThenTheReportsCardLinks(string tab)
        {
            var slug = ComplianceTracker.Web.Models.ProjectTabs.All.Single(t => t.Title == tab).Slug;
            var href = _web.Page!.QuerySelector("#project-reports a#reports-link")?.GetAttribute("href") ?? "";
            Assert.EndsWith("/" + slug, href);
            Assert.DoesNotContain("Traceability matrix (CSV)", _web.Page!.QuerySelector("#project-reports")!.TextContent);
        }

        [Then("the download is an Excel file")]
        public void ThenTheDownloadIsExcel()
        {
            Assert.True(_web.Response!.IsSuccessStatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _web.Response.Content.Headers.ContentType?.MediaType);
        }

        private IReadOnlyList<IElement> Reports()
            => _web.Page!.QuerySelectorAll("#project-reports-tab .report").ToList();

        private IElement Report(string title)
            => Reports().FirstOrDefault(r => r.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Reports tab has no report '{title}'.");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
