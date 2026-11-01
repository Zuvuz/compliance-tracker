using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-035: framework versions with a start date, through the web app with test sign-in and the scenario's clock.
    // UI contract: the "applies from" date fields (name appliesFrom) on the upload forms and /Frameworks/SetAppliesFrom,
    // the status texts on /Frameworks/Versions/{id} (.framework-status) and on the project's Frameworks tab,
    // and the "Prepare for version N" form (.prepare-upcoming).
    [Binding]
    public class VersionStartDateStepDefinitions
    {
        private readonly WebScenario _web;

        public VersionStartDateStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        // The date is written as on the page, dd-MM-yyyy; the form field takes yyyy-MM-dd
        [When("I upload version 2 of {string} that applies from {string} and activate it")]
        [Given("version 2 of {string} applies from {string} and is active")]
        public async Task WhenIUploadAVersionThatAppliesFrom(string frameworkName, string date)
        {
            await _web.OpenAsync("/Frameworks");
            var row = _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);

            await _web.PostFileAsync("/Frameworks/UploadVersion", new Dictionary<string, string>
            {
                ["FrameworkId"] = row.GetAttribute("data-framework-id")!,
                ["appliesFrom"] = FormDate(date)
            }, "new-version.pdf", TestFiles.Pdf());

            await FrameworkUi.ActivateUploadedDraftAsync(_web);
        }

        // R5: the "Prepare for version N" button on the project's Frameworks tab
        [When("I move {string} to version {int} of {string}")]
        public async Task WhenIMoveToVersion(string projectName, int version, string frameworkName)
        {
            await OpenFrameworksTabAsync(projectName);
            var form = FrameworkRow(frameworkName).QuerySelector("form.prepare-upcoming")
                ?? throw new Xunit.Sdk.XunitException($"'{projectName}' offers no upcoming version of '{frameworkName}'.");
            Assert.Contains($"version {version}", form.TextContent);

            await _web.PostFormAsync("/Projects/UseNewest", form.QuerySelectorAll("input[type=hidden]")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value")!));
        }

        // R2: the page offers no date field for an active version, so the request is sent directly
        [When("I try to change the start date of version {int} of {string} to {string}")]
        public async Task WhenITryToChangeTheStartDate(int version, string frameworkName, string date)
        {
            var versionId = await VersionIdAsync(frameworkName, version);
            await FrameworkUi.OpenVersionAsync(_web, versionId);
            await _web.PostFormAsync("/Frameworks/SetAppliesFrom", new Dictionary<string, string>
            {
                ["versionId"] = versionId,
                ["appliesFrom"] = FormDate(date)
            });
        }

        // ---------------------------------------------------------- Then

        [Then("the current version of {string} is version {int}")]
        public async Task ThenTheCurrentVersionIs(string frameworkName, int version)
        {
            var rows = await VersionRowsAsync(frameworkName);
            var current = rows.Where(r => Cell(r, "framework-status")!.Contains("current")).Select(r => Cell(r, "framework-version"));
            Assert.Equal([version.ToString()], current);
        }

        [Then("version {int} of {string} is shown as {string}")]
        public async Task ThenVersionIsShownAs(int version, string frameworkName, string status)
        {
            var row = (await VersionRowsAsync(frameworkName)).Single(r => Cell(r, "framework-version") == version.ToString());
            Assert.Equal(status, Cell(row, "framework-status"));

            // The framework list shows the same for its newest upload
            await _web.OpenAsync("/Frameworks");
            var listRow = _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);
            Assert.Equal(status, Cell(listRow, "framework-status"));
        }

        [Then("the Frameworks tab of {string} shows {string} version {int} as {string}")]
        public async Task ThenTheFrameworksTabShowsVersionAs(string projectName, string frameworkName, int version, string status)
        {
            await OpenFrameworksTabAsync(projectName);
            var row = FrameworkRow(frameworkName);
            Assert.Equal(version.ToString(), Cell(row, "framework-version"));
            Assert.StartsWith(status, Cell(row, "framework-status"));
        }

        [Then("the Frameworks tab of {string} shows {string} as {string}")]
        public async Task ThenTheFrameworksTabShowsAs(string projectName, string frameworkName, string status)
        {
            await OpenFrameworksTabAsync(projectName);
            Assert.Equal(status, Cell(FrameworkRow(frameworkName), "framework-status"));
        }

        // ---------------------------------------------------------- Helpers

        private static string FormDate(string date)
            => DateOnly.ParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string? Cell(IElement row, string className) => row.QuerySelector($".{className}")?.TextContent.Trim();

        private async Task<IReadOnlyList<IElement>> VersionRowsAsync(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var frameworkId = await db.Frameworks.Where(f => f.Name == frameworkName).Select(f => f.Id).SingleAsync();
            await _web.OpenAsync($"/Frameworks/Versions/{frameworkId}");
            return _web.Page!.QuerySelectorAll("#versions tbody tr").ToList();
        }

        private async Task OpenFrameworksTabAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/frameworks");
        }

        private IElement FrameworkRow(string frameworkName)
            => _web.Page!.QuerySelectorAll("#project-frameworks tbody tr")
                   .FirstOrDefault(r => Cell(r, "framework-name") == frameworkName)
               ?? throw new Xunit.Sdk.XunitException($"The Frameworks tab has no row for '{frameworkName}'.");

        private async Task<string> VersionIdAsync(string frameworkName, int version)
        {
            await using var db = _web.CreateDbContext();
            return (await (
                    from v in db.FrameworkVersions
                    join f in db.Frameworks on v.FrameworkId equals f.Id
                    where f.Name == frameworkName && v.Number == version
                    select v.Id)
                .SingleAsync()).ToString();
        }
    }
}
