using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-006: switching built-in frameworks (CRA, AI Act) on and off, tested through the web app with test sign-in.
    // UI contract: the page /Frameworks/BuiltIn with the table #built-in-frameworks, rows with data-framework-id,
    // cells framework-name and framework-state ("On"/"Off"), and the forms /Frameworks/Enable and /Frameworks/Disable.
    // On /Frameworks, the cell framework-source says "Built-in" or "Uploaded".
    [Binding]
    public class BuiltInFrameworksStepDefinitions
    {
        private const string BuiltInPath = "/Frameworks/BuiltIn";
        private const string ListPath = "/Frameworks";

        private readonly WebScenario _web;

        public BuiltInFrameworksStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("{string} is switched on")]
        public Task GivenIsSwitchedOn(string frameworkName)
            => Switch(frameworkName, on: true);

        // ---------------------------------------------------------- When

        [When("I open the built-in frameworks page")]
        public Task WhenIOpenTheBuiltInFrameworksPage()
            => _web.OpenAsync(BuiltInPath);

        [When("I switch {string} on")]
        public Task WhenISwitchOn(string frameworkName)
            => Switch(frameworkName, on: true);

        [When("I switch {string} off")]
        public Task WhenISwitchOff(string frameworkName)
            => Switch(frameworkName, on: false);

        // R5: tries the "Upload new version" request directly, as the page offers no such form for built-ins
        [When("I try to upload a new version of {string}")]
        public async Task WhenITryToUploadANewVersionOf(string frameworkName)
        {
            await _web.OpenAsync(BuiltInPath);
            var frameworkId = BuiltInRow(frameworkName).GetAttribute("data-framework-id")!;

            await _web.OpenAsync(ListPath);
            await _web.PostFileAsync(ListPath + "/UploadVersion",
                new Dictionary<string, string> { ["FrameworkId"] = frameworkId },
                "cra.pdf", TestFiles.Pdf());
        }

        // ---------------------------------------------------------- Then

        [Then("I see {string} switched off")]
        public void ThenISeeSwitchedOff(string frameworkName)
            => Assert.Equal("Off", BuiltInRow(frameworkName).QuerySelector(".framework-state")!.TextContent.Trim());

        [Then("I see {string} switched on")]
        public void ThenISeeSwitchedOn(string frameworkName)
            => Assert.Equal("On", BuiltInRow(frameworkName).QuerySelector(".framework-state")!.TextContent.Trim());

        // R3: switched-on built-ins are listed next to the uploaded ones, marked as built-in
        [Then("the framework list shows {string} marked {string}")]
        public async Task ThenTheFrameworkListShowsMarked(string frameworkName, string source)
        {
            await _web.OpenAsync(ListPath);
            var row = ListRow(frameworkName)
                ?? throw new Xunit.Sdk.XunitException($"The framework list has no row for '{frameworkName}'.");

            Assert.Equal(source, row.QuerySelector(".framework-source")!.TextContent.Trim());
        }

        [Then("the framework list does not show {string}")]
        public async Task ThenTheFrameworkListDoesNotShow(string frameworkName)
        {
            await _web.OpenAsync(ListPath);
            Assert.Null(ListRow(frameworkName));
        }

        // R5: an error is shown, and the built-in framework still has only its one version
        [Then("the request is rejected")]
        public async Task ThenTheRequestIsRejected()
        {
            Assert.NotEmpty(_web.Page!.QuerySelectorAll(".validation-summary-errors li"));

            await using var db = _web.CreateDbContext();
            var builtInVersions = await db.FrameworkVersions
                .Where(v => db.Frameworks.Any(f => f.Id == v.FrameworkId && f.OwnerOrganizationId == null))
                .CountAsync();
            Assert.Equal(2, builtInVersions); // CRA and the AI Act, one version each
        }

        // US-016 R5: the "switched on" record is still there, now with the time it was switched off
        [Then("the history shows {string} was switched on and then off")]
        public async Task ThenTheHistoryShowsSwitchedOnAndThenOff(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var versionIds = await db.FrameworkVersions
                .Where(v => db.Frameworks.Any(f => f.Id == v.FrameworkId && f.Name == frameworkName && f.OwnerOrganizationId == null))
                .Select(v => v.Id)
                .ToListAsync();

            var record = Assert.Single(await db.OrganizationFrameworks
                .Where(s => versionIds.Contains(s.FrameworkVersionId))
                .ToListAsync());
            Assert.NotNull(record.DisabledAt);
            Assert.NotNull(record.DisabledById);
            Assert.True(record.EnabledAt <= record.DisabledAt);
        }

        // US-016 R6: the row links to the official text
        [Then("{string} links to {string}")]
        public void ThenLinksTo(string frameworkName, string url)
        {
            var links = BuiltInRow(frameworkName).QuerySelectorAll("a[href]").Select(a => a.GetAttribute("href"));
            Assert.Contains(url, links);
        }

        // ---------------------------------------------------------- Helpers

        // Uses the On/Off form on the built-in frameworks page, like a user would
        private async Task Switch(string frameworkName, bool on)
        {
            await _web.OpenAsync(BuiltInPath);
            var frameworkId = BuiltInRow(frameworkName).GetAttribute("data-framework-id")!;

            await _web.PostFormAsync(on ? "/Frameworks/Enable" : "/Frameworks/Disable",
                new Dictionary<string, string> { ["FrameworkId"] = frameworkId });
        }

        // The framework's row on the built-in frameworks page (the page must be open)
        private IElement BuiltInRow(string frameworkName)
            => _web.Page!.QuerySelectorAll("#built-in-frameworks tbody tr")
                .SingleOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName)
               ?? throw new Xunit.Sdk.XunitException($"The built-in frameworks page has no row for '{frameworkName}'.");

        // The framework's row in the organisation's framework list, or null (the list must be open)
        private IElement? ListRow(string frameworkName)
            => _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                .SingleOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);
    }
}
