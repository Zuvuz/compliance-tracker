using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-011: choosing frameworks for projects, tested through the web app with test sign-in.
    // UI contract:
    // - the create-project form has checkboxes FrameworkIds (class framework-option) with the framework's name as label;
    // - the project page has #project-frameworks with rows data-framework-id, cells framework-name and
    //   framework-version, and the forms /Projects/AddFramework, /Projects/UseNewest and /Projects/RemoveFramework;
    // - new versions are uploaded with the "Upload new version" form on /Frameworks and activated on their page (US-007);
    // - the organisation page has #organisation-frameworks with rows framework-usage, cells framework-name,
    //   framework-version and links usage-project, and the badge framework-outdated.
    [Binding]
    public class ProjectFrameworksStepDefinitions
    {
        private readonly WebScenario _web;

        public ProjectFrameworksStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("{string} and {string} use {string} version {int}")]
        public async Task GivenBothUseVersion(string firstProject, string secondProject, string frameworkName, int version)
        {
            await GivenUsesVersion(firstProject, frameworkName, version);
            await GivenUsesVersion(secondProject, frameworkName, version);
        }

        // Uploads and activates new versions until the version exists, then adds exactly that version
        [Given("{string} uses {string} version {int}")]
        public async Task GivenUsesVersion(string projectName, string frameworkName, int version)
        {
            while (await NewestVersionNumberAsync(frameworkName) < version)
            {
                await UploadAndActivateNewVersionAsync(frameworkName);
            }

            await AddFrameworkAsync(projectName, frameworkName, version);
        }

        // A new active version that no project is moved to yet
        [Given("a new version of {string} has been uploaded and activated")]
        [When("a new version of {string} is uploaded and activated")]
        public Task GivenANewVersionHasBeenUploadedAndActivated(string frameworkName)
            => UploadAndActivateNewVersionAsync(frameworkName);

        [Given("{string} uses {string}")]
        public Task GivenUses(string projectName, string frameworkName)
            => AddFrameworkAsync(projectName, frameworkName, version: null);

        // ---------------------------------------------------------- When

        // R2: ticks the framework on the create-project form
        [When("I create the project {string} using {string}")]
        public async Task WhenICreateTheProjectUsing(string projectName, string frameworkName)
        {
            await _web.OpenAsync("/Projects/Create");
            var checkbox = _web.Page!.QuerySelectorAll("input.framework-option")
                .SingleOrDefault(c => _web.Page.QuerySelector($"label[for='{c.Id}']")?.TextContent.Trim() == frameworkName)
                ?? throw new Xunit.Sdk.XunitException($"The create-project form has no checkbox for '{frameworkName}'.");

            await _web.PostFormAsync("/Projects/Create", new Dictionary<string, string>
            {
                ["Name"] = projectName,
                ["Description"] = "",
                ["IntendedPurpose"] = "Test purpose",
                ["Scope"] = "",
                ["LifecyclePhase"] = "Planning",
                ["FrameworkIds"] = checkbox.GetAttribute("value")!
            });
        }

        [When("I add {string} to the project {string}")]
        public Task WhenIAddToTheProject(string frameworkName, string projectName)
            => AddFrameworkAsync(projectName, frameworkName, version: null);

        // R3: the page only offers the newest version, so this sends the request for an older one directly
        [When("I add {string} version {int} to the project {string}")]
        public Task WhenIAddVersionToTheProject(string frameworkName, int version, string projectName)
            => AddFrameworkAsync(projectName, frameworkName, version);

        [When("I open the organisation page")]
        public Task WhenIOpenTheOrganisationPage()
            => _web.OpenAsync("/Organizations/Mine");

        // R6: the "Use newest" button on the project page
        [When("I move {string} to the newest version of {string}")]
        public async Task WhenIMoveToTheNewestVersion(string projectName, string frameworkName)
        {
            var projectId = await OpenProjectAsync(projectName);
            var frameworkId = FrameworkRow(frameworkName)?.GetAttribute("data-framework-id")
                ?? throw new Xunit.Sdk.XunitException($"'{projectName}' does not use '{frameworkName}'.");

            await _web.PostFormAsync("/Projects/UseNewest", new Dictionary<string, string>
            {
                ["ProjectId"] = projectId.ToString(),
                ["FrameworkId"] = frameworkId
            });
        }

        // R7: the "Remove" button on the project page
        [When("I remove {string} from the project {string}")]
        public async Task WhenIRemoveFromTheProject(string frameworkName, string projectName)
        {
            var projectId = await OpenProjectAsync(projectName);
            var frameworkId = FrameworkRow(frameworkName)?.GetAttribute("data-framework-id")
                ?? throw new Xunit.Sdk.XunitException($"'{projectName}' does not use '{frameworkName}'.");

            await _web.PostFormAsync("/Projects/RemoveFramework", new Dictionary<string, string>
            {
                ["ProjectId"] = projectId.ToString(),
                ["FrameworkId"] = frameworkId
            });
        }

        // ---------------------------------------------------------- Then

        [Then("the project {string} uses {string} version {int}")]
        public async Task ThenTheProjectUsesVersion(string projectName, string frameworkName, int version)
        {
            await OpenProjectAsync(projectName);
            var row = FrameworkRow(frameworkName)
                ?? throw new Xunit.Sdk.XunitException($"The page of '{projectName}' does not list '{frameworkName}'.");

            Assert.Equal(version.ToString(), row.QuerySelector(".framework-version")!.TextContent.Trim());
        }

        [Then("the project {string} does not use {string}")]
        public async Task ThenTheProjectDoesNotUse(string projectName, string frameworkName)
        {
            await OpenProjectAsync(projectName);
            Assert.Null(FrameworkRow(frameworkName));
        }

        [Then("I see {string} version {int} used by {string}")]
        public void ThenISeeVersionUsedBy(string frameworkName, int version, string projectName)
            => Assert.Contains(projectName, ProjectsIn(UsageRow(frameworkName, version)));

        [Then("I see {string} version {int} used by {string} marked {string}")]
        public void ThenISeeVersionUsedByMarked(string frameworkName, int version, string projectName, string marker)
        {
            var row = UsageRow(frameworkName, version);
            Assert.Contains(projectName, ProjectsIn(row));
            Assert.Equal(marker, row.QuerySelector(".framework-outdated")?.TextContent.Trim());
        }

        // R6: after moving, the old version disappears from the overview because no project uses it
        [Then("the organisation page shows only {string} version {int}, used by {string} and {string}")]
        public async Task ThenTheOrganisationPageShowsOnly(string frameworkName, int version, string firstProject, string secondProject)
        {
            await WhenIOpenTheOrganisationPage();
            var rows = UsageRows(frameworkName).ToList();

            var row = Assert.Single(rows);
            Assert.Equal(version.ToString(), row.QuerySelector(".framework-version")!.TextContent.Trim());
            Assert.Equal(new[] { firstProject, secondProject }.Order(), ProjectsIn(row).Order());
            Assert.Null(row.QuerySelector(".framework-outdated"));
        }

        // US-016 R9
        [Then("the page of {string} says {string} has a newer version")]
        public async Task ThenThePageSaysHasANewerVersion(string projectName, string frameworkName)
        {
            await OpenProjectAsync(projectName);
            var row = FrameworkRow(frameworkName)
                ?? throw new Xunit.Sdk.XunitException($"The page of '{projectName}' does not list '{frameworkName}'.");

            Assert.Equal("newer version available", row.QuerySelector(".framework-newer")?.TextContent.Trim());
        }

        // US-016 R10: the link is still in the database, with the time it was removed
        [Then("the history of {string} shows {string} version {int} was added and then removed")]
        public async Task ThenTheHistoryShowsAddedAndThenRemoved(string projectName, string frameworkName, int version)
        {
            var (frameworkId, versionId) = await IdsAsync(frameworkName, version);
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();

            var link = Assert.Single(await db.ProjectFrameworks
                .Where(f => f.ProjectId == projectId && f.FrameworkId == frameworkId)
                .ToListAsync());
            Assert.Equal(versionId, link.FrameworkVersionId);
            Assert.NotNull(link.RemovedAt);
            Assert.NotNull(link.RemovedById);
            Assert.True(link.AddedAt <= link.RemovedAt);
        }

        // ---------------------------------------------------------- Helpers

        // Opens the project page and returns the project's id
        private async Task<Guid> OpenProjectAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}");
            return id;
        }

        // Adds a framework through the project page's form; with a version, that exact version is sent
        private async Task AddFrameworkAsync(string projectName, string frameworkName, int? version)
        {
            var projectId = await OpenProjectAsync(projectName);
            var (frameworkId, versionId) = await IdsAsync(frameworkName, version);

            var fields = new Dictionary<string, string>
            {
                ["ProjectId"] = projectId.ToString(),
                ["FrameworkId"] = frameworkId.ToString()
            };
            if (versionId is not null)
            {
                fields["FrameworkVersionId"] = versionId.Value.ToString();
            }

            await _web.PostFormAsync("/Projects/AddFramework", fields);
        }

        // Uses the "Upload new version" form on /Frameworks, then activates the new draft (US-007)
        private async Task UploadAndActivateNewVersionAsync(string frameworkName)
        {
            await _web.OpenAsync("/Frameworks");
            var row = _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);

            var fields = new Dictionary<string, string> { ["FrameworkId"] = row.GetAttribute("data-framework-id")! };
            await _web.PostFileAsync("/Frameworks/UploadVersion", fields, "new-version.pdf", TestFiles.Pdf());
            await FrameworkUi.ActivateUploadedDraftAsync(_web);
        }

        // The framework's id and, if asked for, the id of one of its versions (own or built-in framework)
        private async Task<(Guid FrameworkId, Guid? VersionId)> IdsAsync(string frameworkName, int? version)
        {
            await using var db = _web.CreateDbContext();
            var framework = await db.Frameworks.SingleAsync(f => f.Name == frameworkName);
            var versionId = version is null ? (Guid?)null : framework.Versions.Single(v => v.Number == version).Id;
            return (framework.Id, versionId);
        }

        private async Task<int> NewestVersionNumberAsync(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var framework = await db.Frameworks.SingleAsync(f => f.Name == frameworkName);
            return framework.LatestVersion.Number;
        }

        // The framework's row on the project page, or null (the page must be open)
        private IElement? FrameworkRow(string frameworkName)
            => _web.Page!.QuerySelectorAll("#project-frameworks tbody tr")
                .SingleOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);

        // The framework's lines in the organisation overview (the page must be open)
        private IEnumerable<IElement> UsageRows(string frameworkName)
            => _web.Page!.QuerySelectorAll("#organisation-frameworks tr.framework-usage")
                .Where(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);

        private IElement UsageRow(string frameworkName, int version)
            => UsageRows(frameworkName)
                .SingleOrDefault(r => r.QuerySelector(".framework-version")?.TextContent.Trim() == version.ToString())
               ?? throw new Xunit.Sdk.XunitException($"The organisation page has no line for '{frameworkName}' version {version}.");

        private static IEnumerable<string> ProjectsIn(IElement row)
            => row.QuerySelectorAll("a.usage-project").Select(a => a.TextContent.Trim());
    }
}
