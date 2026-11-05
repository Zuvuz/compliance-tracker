using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-007: requirements found in a framework PDF, checked as a draft and locked by activation.
    // Tested through the web app with test sign-in and real PDFs made with PdfPig (TestFiles.PdfWithText).
    // UI contract (Views/Frameworks/Version.cshtml): #version-status, #version-current, #requirements with rows
    // data-requirement-code / data-requirement-id and inputs or cells requirement-code / requirement-title,
    // the forms /Frameworks/AddRequirement, ChangeRequirement, RemoveRequirement and Activate, and #no-requirements.
    [Binding]
    public class ExtractRequirementsStepDefinitions
    {
        private readonly WebScenario _web;

        public ExtractRequirementsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        // "A.5.15 Access contr": the PDF has one line with that code and title
        [Given("the framework {string} has a draft with {string}")]
        public Task GivenTheFrameworkHasADraftWith(string frameworkName, string line)
            => AddFrameworkAsync(frameworkName, TestFiles.PdfWithText(line));

        [Given("version {int} of {string} is active")]
        public async Task GivenVersionIsActive(int version, string frameworkName)
        {
            await EnsureVersionAsync(frameworkName, version);
            await WhenIActivateVersion(version, frameworkName);
        }

        [Given("version {int} of {string} is a draft")]
        public Task GivenVersionIsADraft(int version, string frameworkName)
            => EnsureVersionAsync(frameworkName, version);

        // ---------------------------------------------------------- When

        [When("I add the framework {string} with a PDF containing:")]
        public Task WhenIAddTheFrameworkWithAPdfContaining(string frameworkName, DataTable lines)
            => AddFrameworkAsync(frameworkName, TestFiles.PdfWithText(lines.Rows.Select(r => r["line"]).ToArray()));

        // R5: like a scanned document, there is no text to read
        [When("I add the framework {string} with a PDF without text")]
        public Task WhenIAddTheFrameworkWithAPdfWithoutText(string frameworkName)
            => AddFrameworkAsync(frameworkName, TestFiles.PdfWithText());

        [When("I change the title of {string} to {string}")]
        public Task WhenIChangeTheTitle(string code, string title)
            => ChangeTitleAsync(code, title);

        // R3: the page offers no form for an active version, so the request is sent directly
        [When("I try to change the title of {string}")]
        public Task WhenITryToChangeTheTitle(string code)
            => ChangeTitleAsync(code, "Changed after activation");

        [When("I delete the requirement {string}")]
        public async Task WhenIDeleteTheRequirement(string code)
        {
            var versionId = FrameworkUi.OpenVersionId(_web);
            await _web.PostFormAsync("/Frameworks/RemoveRequirement", new Dictionary<string, string>
            {
                ["versionId"] = versionId,
                ["requirementId"] = RequirementRow(code).GetAttribute("data-requirement-id")!
            });
        }

        [When("I add the requirement {string} with the title {string}")]
        public Task WhenIAddTheRequirement(string code, string title)
            => FrameworkUi.AddRequirementAsync(_web, FrameworkUi.OpenVersionId(_web), code, title);

        [When("I activate version {int} of {string}")]
        public async Task WhenIActivateVersion(int version, string frameworkName)
        {
            var versionId = await VersionIdAsync(frameworkName, version);
            await FrameworkUi.OpenVersionAsync(_web, versionId);
            await FrameworkUi.ActivateAsync(_web, versionId);
        }

        [When("{string} opens version {int} of {string}")]
        public async Task WhenOpensVersion(string userName, int version, string frameworkName)
        {
            var versionId = await VersionIdAsync(frameworkName, version);
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync($"/Frameworks/Version/{versionId}");
        }

        // ---------------------------------------------------------- Then

        [Then("the draft of {string} version {int} lists {string} and {string}")]
        public async Task ThenTheDraftLists(string frameworkName, int version, string first, string second)
        {
            await FrameworkUi.OpenVersionAsync(_web, await VersionIdAsync(frameworkName, version));

            Assert.Equal("Draft", _web.Page!.QuerySelector("#version-status")?.TextContent.Trim());
            Assert.Equal([first, second], Codes());
        }

        [Then("the requirement {string} has the title {string}")]
        [Then("the draft lists {string} with the title {string}")]
        public void ThenTheRequirementHasTheTitle(string code, string title)
            => Assert.Equal(title, Value(RequirementRow(code).QuerySelector(".requirement-title")));

        [Then("the draft lists only {string}")]
        public void ThenTheDraftListsOnly(string code)
            => Assert.Equal([code], Codes());

        [Then("the page of version {int} of {string} offers no changes")]
        public async Task ThenThePageOffersNoChanges(int version, string frameworkName)
        {
            await FrameworkUi.OpenVersionAsync(_web, await VersionIdAsync(frameworkName, version));

            Assert.Equal("Active", _web.Page!.QuerySelector("#version-status")?.TextContent.Trim());
            Assert.Empty(_web.Page!.QuerySelectorAll("#requirements input, #add-requirement, #activate-version"));
            Assert.Equal("Access control", Value(RequirementRow("A.5.15").QuerySelector(".requirement-title")));
        }

        [Then("version {int} is the current version of {string}")]
        public async Task ThenVersionIsCurrent(int version, string frameworkName)
        {
            await FrameworkUi.OpenVersionAsync(_web, await VersionIdAsync(frameworkName, version));
            Assert.NotNull(_web.Page!.QuerySelector("#version-current"));
        }

        [Then("version {int} of {string} is still active")]
        public async Task ThenVersionIsStillActive(int version, string frameworkName)
        {
            await FrameworkUi.OpenVersionAsync(_web, await VersionIdAsync(frameworkName, version));
            Assert.Equal("Active", _web.Page!.QuerySelector("#version-status")?.TextContent.Trim());
            Assert.Null(_web.Page!.QuerySelector("#version-current"));
        }

        [Then("I see {string}")]
        public void ThenISee(string text)
            => Assert.Contains(text, _web.Page!.Body!.TextContent.Split('\n').Select(l => l.Trim()));

        // Neither the create-project form nor a project's "Add framework" list offers a framework with only a draft
        [Then("{string} cannot be chosen for a project")]
        public async Task ThenCannotBeChosenForAProject(string frameworkName)
        {
            await _web.OpenAsync("/Projects/Create");
            Assert.DoesNotContain(frameworkName, _web.Page!.QuerySelectorAll("label").Select(l => l.TextContent.Trim()));

            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Select(p => p.Id).FirstAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}/frameworks");
            Assert.DoesNotContain(frameworkName, _web.Page!.QuerySelectorAll("#add-framework option").Select(o => o.TextContent.Trim()));
        }

        // ---------------------------------------------------------- Helpers

        // The "Add" form on /Frameworks; the app then shows the new draft
        private async Task AddFrameworkAsync(string frameworkName, byte[] pdf)
        {
            await _web.OpenAsync("/Frameworks");
            await _web.PostFileAsync("/Frameworks/Add",
                new Dictionary<string, string> { ["Name"] = frameworkName }, "framework.pdf", pdf);
        }

        // Uploads versions until the framework has this one. The first version gets a requirement, so it can be activated.
        private async Task EnsureVersionAsync(string frameworkName, int version)
        {
            if (!await FrameworkExistsAsync(frameworkName))
            {
                await GivenTheFrameworkHasADraftWith(frameworkName, "A.5.15 Access control");
            }

            while (await NewestNumberAsync(frameworkName) < version)
            {
                await _web.OpenAsync("/Frameworks");
                var row = _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                    .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);
                await _web.PostFileAsync("/Frameworks/UploadVersion",
                    new Dictionary<string, string> { ["FrameworkId"] = row.GetAttribute("data-framework-id")! },
                    "framework.pdf", TestFiles.PdfWithText("A.5.15 Access control"));
            }
        }

        private async Task ChangeTitleAsync(string code, string title)
        {
            var row = RequirementRow(code);
            await _web.PostFormAsync("/Frameworks/ChangeRequirement", new Dictionary<string, string>
            {
                ["versionId"] = FrameworkUi.OpenVersionId(_web),
                ["requirementId"] = row.GetAttribute("data-requirement-id")!,
                ["code"] = code,
                ["title"] = title,
                ["description"] = ""
            });
        }

        // The codes on the open version page, in the page's order
        private IEnumerable<string> Codes()
            => _web.Page!.QuerySelectorAll("#requirements tbody tr").Select(r => r.GetAttribute("data-requirement-code")!);

        private IElement RequirementRow(string code)
            => _web.Page!.QuerySelector($"#requirements tr[data-requirement-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} lists no requirement '{code}'.");

        // An input in a draft, or a plain cell in an active version
        private static string? Value(IElement? element)
            => element is IHtmlInputElement input ? input.Value : element?.TextContent.Trim();

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

        private async Task<bool> FrameworkExistsAsync(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Frameworks.AnyAsync(f => f.Name == frameworkName);
        }

        private async Task<int> NewestNumberAsync(string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            return (await db.Frameworks.SingleAsync(f => f.Name == frameworkName)).LatestVersion.Number;
        }
    }
}
