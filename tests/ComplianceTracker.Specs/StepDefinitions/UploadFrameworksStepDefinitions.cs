using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-005: uploading frameworks as PDF, tested through the web app with test sign-in.
    // The steps define the UI "contract": the page /Frameworks, rows with data-framework-id,
    // cells with the classes framework-name / framework-version / framework-file, and links with open-pdf.
    [Binding]
    public class UploadFrameworksStepDefinitions
    {
        private const string ListPath = "/Frameworks";

        private readonly WebScenario _web;

        // Addresses of PDFs owned by another organisation, so the test can try to open them as Anna
        private readonly Dictionary<string, string> _otherOrganisationsPdfLinks = new();

        public UploadFrameworksStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("the framework {string} has been uploaded")]
        public Task GivenTheFrameworkHasBeenUploaded(string name)
            => AddFramework(name, "upload.pdf", TestFiles.Pdf());

        // US-007: projects can only use an active version
        [Given("the framework {string} has been uploaded and activated")]
        public async Task GivenTheFrameworkHasBeenUploadedAndActivated(string name)
        {
            await AddFramework(name, "upload.pdf", TestFiles.Pdf());
            await FrameworkUi.ActivateUploadedDraftAsync(_web);
        }

        [Given("{string} has created the organisation {string} with the framework {string}")]
        public async Task GivenHasCreatedTheOrganisationWithTheFramework(string userName, string organisationName, string frameworkName)
        {
            await _web.CreateOrganisationAsync(userName, organisationName);

            // The other user uploads in their own "browser"
            using var other = _web.CreateClient(signedInAs: userName);
            var listPage = await WebScenario.ParseAsync(await other.GetAsync(ListPath));
            var token = listPage.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;

            using var form = new MultipartFormDataContent
            {
                { new StringContent(frameworkName), "Name" },
                { new StringContent(token), "__RequestVerificationToken" },
                { new ByteArrayContent(TestFiles.Pdf()), "File", "beta.pdf" }
            };
            var afterUpload = await WebScenario.ParseAsync(await other.PostAsync(ListPath + "/Add", form));

            // Remember the PDF's address, so Anna can try to open it (the page after an upload is the new draft, US-007)
            _otherOrganisationsPdfLinks[frameworkName] = afterUpload.QuerySelector("a.open-pdf")!.GetAttribute("href")!;
        }

        // ---------------------------------------------------------- When

        [When("I add the framework {string} with the file {string}")]
        public Task WhenIAddTheFrameworkWithTheFile(string name, string fileName)
            => AddFramework(name, fileName, TestFiles.Pdf());

        // The next two steps come from the Scenario Outline examples about file type and size (R5)
        [When("I add the framework {string} with a text file named {string}")]
        public Task WhenIAddTheFrameworkWithATextFile(string name, string fileName)
            => AddFramework(name, fileName, TestFiles.TextFile());

        [When("I add the framework {string} with a PDF of {int} MB named {string}")]
        public Task WhenIAddTheFrameworkWithALargePdf(string name, int megabytes, string fileName)
            => AddFramework(name, fileName, TestFiles.PdfOfMegabytes(megabytes));

        [When("I upload a new version of {string} with the file {string}")]
        public async Task WhenIUploadANewVersion(string name, string fileName)
        {
            // Uses the "Upload new version" form on the framework's row
            await _web.OpenAsync(ListPath);
            var frameworkId = RowFor(_web.Page!, name).GetAttribute("data-framework-id")!;

            await _web.PostFileAsync(ListPath + "/UploadVersion",
                new Dictionary<string, string> { ["FrameworkId"] = frameworkId },
                fileName, TestFiles.Pdf());
        }

        [When("I open the PDF of {string}")]
        public Task WhenIOpenThePdfOf(string frameworkName)
            => _web.OpenAsync(_otherOrganisationsPdfLinks[frameworkName]);

        // ---------------------------------------------------------- Then

        [Then("the framework list shows {string} with version {int}")]
        public async Task ThenTheListShowsWithVersion(string name, int version)
        {
            await _web.OpenAsync(ListPath);
            var row = RowFor(_web.Page!, name);
            Assert.Equal(version.ToString(), row.QuerySelector(".framework-version")!.TextContent.Trim());
        }

        [Then("the framework list shows {string} and {string}")]
        public async Task ThenTheListShows(string first, string second)
        {
            await _web.OpenAsync(ListPath);
            RowFor(_web.Page!, first);
            RowFor(_web.Page!, second);
        }

        [Then("version {int} of {string} can still be opened")]
        public async Task ThenVersionCanStillBeOpened(int version, string name)
        {
            await _web.OpenAsync(ListPath);
            var frameworkId = RowFor(_web.Page!, name).GetAttribute("data-framework-id");

            // Find the old version on the versions page and download it
            await _web.OpenAsync($"{ListPath}/Versions/{frameworkId}");
            var versionRow = _web.Page!.QuerySelectorAll("tbody tr")
                .Single(r => r.QuerySelector(".framework-version")!.TextContent.Trim() == version.ToString());

            var pdf = await _web.Client.GetAsync(versionRow.QuerySelector("a.open-pdf")!.GetAttribute("href"));
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }

        [Then("the framework list shows the file name {string}")]
        public async Task ThenTheListShowsTheFileName(string fileName)
        {
            await _web.OpenAsync(ListPath);
            Assert.Contains(_web.Page!.QuerySelectorAll(".framework-file"), cell => cell.TextContent.Trim() == fileName);
        }

        [Then("opening the PDF returns the file name {string}")]
        public async Task ThenOpeningThePdfReturnsTheFileName(string fileName)
        {
            await _web.OpenAsync(ListPath);
            var link = _web.Page!.QuerySelector("a.open-pdf")!.GetAttribute("href");

            // ASVS V5.4.1: the download's Content-Disposition has our cleaned name, not the uploaded one
            var pdf = await _web.Client.GetAsync(link);
            Assert.Equal(fileName, pdf.Content.Headers.ContentDisposition?.FileNameStar
                ?? pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        }

        [Then("the activity log for {string} has {string} for {string} concerning {string} version {int}")]
        public async Task ThenTheActivityLogHasConcerning(string organisationName, string action, string userName, string frameworkName, int version)
        {
            await using var db = _web.CreateDbContext();

            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var user = organisation.Users.Single(u => u.Name == userName);

            // The id of exactly the version the log entry should be about
            var versionId = await (
                    from v in db.FrameworkVersions
                    join f in db.Frameworks on v.FrameworkId equals f.Id
                    where f.OwnerOrganizationId == organisation.Id && f.Name == frameworkName && v.Number == version
                    select v.Id)
                .SingleAsync();

            var expectedAction = Enum.Parse<ActivityAction>(action);

            // R8: who (user), what (action), where (organisation) and about what (SubjectId) all match
            Assert.True(
                await db.ActivityLog.AnyAsync(e =>
                    e.OrganizationId == organisation.Id &&
                    e.UserId == user.Id &&
                    e.Action == expectedAction &&
                    e.SubjectId == versionId),
                $"No '{action}' log entry by {userName} for {frameworkName} version {version}.");
        }

        // ---------------------------------------------------------- Helpers

        // Fills in the "add framework" form and submits it, like a user would
        private async Task AddFramework(string name, string fileName, byte[] content)
        {
            await _web.OpenAsync(ListPath);
            await _web.PostFileAsync(ListPath + "/Add",
                new Dictionary<string, string> { ["Name"] = name },
                fileName, content);
        }

        // The framework's row in the list, with a clear error if it is missing
        private static IElement RowFor(IDocument page, string frameworkName)
            => page.QuerySelectorAll("#frameworks tbody tr")
                .SingleOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName)
               ?? throw new Xunit.Sdk.XunitException($"The framework list has no row for '{frameworkName}'.");
    }
}