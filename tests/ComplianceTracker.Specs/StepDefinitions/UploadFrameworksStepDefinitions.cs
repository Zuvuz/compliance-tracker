using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-005: upload af frameworks som PDF, testet gennem webappen med test-login
    [Binding]
    public class UploadFrameworksStepDefinitions
    {
        private const string ListPath = "/Frameworks";

        private readonly WebScenario _web;
        private readonly Dictionary<string, string> _otherOrganisationsPdfLinks = new();

        public UploadFrameworksStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("the framework {string} has been uploaded")]
        public Task GivenTheFrameworkHasBeenUploaded(string name)
            => AddFramework(name, "upload.pdf", TestFiles.Pdf());

        [Given("{string} has created the organisation {string} with the framework {string}")]
        public async Task GivenHasCreatedTheOrganisationWithTheFramework(string userName, string organisationName, string frameworkName)
        {
            await _web.CreateOrganisationAsync(userName, organisationName);

            
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

            
            _otherOrganisationsPdfLinks[frameworkName] = RowFor(afterUpload, frameworkName)
                .QuerySelector("a.open-pdf")!.GetAttribute("href")!;
        }

        // ---------------------------------------------------------- When

        [When("I add the framework {string} with the file {string}")]
        public Task WhenIAddTheFrameworkWithTheFile(string name, string fileName)
            => AddFramework(name, fileName, TestFiles.Pdf());

        [When("I add the framework {string} with a text file named {string}")]
        public Task WhenIAddTheFrameworkWithATextFile(string name, string fileName)
            => AddFramework(name, fileName, TestFiles.TextFile());

        [When("I add the framework {string} with a PDF of {int} MB named {string}")]
        public Task WhenIAddTheFrameworkWithALargePdf(string name, int megabytes, string fileName)
            => AddFramework(name, fileName, TestFiles.PdfOfMegabytes(megabytes));

        [When("I upload a new version of {string} with the file {string}")]
        public async Task WhenIUploadANewVersion(string name, string fileName)
        {
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

            var pdf = await _web.Client.GetAsync(link);
            Assert.Equal(fileName, pdf.Content.Headers.ContentDisposition?.FileNameStar
                ?? pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        }

        [Then("the activity log for {string} has {string} for {string} concerning {string} version {int}")]
        public void ThenTheActivityLogHasConcerning(string organisationName, string action, string userName, string frameworkName, int version)
        {
            
            throw new PendingStepException();
        }

       

        private async Task AddFramework(string name, string fileName, byte[] content)
        {
            await _web.OpenAsync(ListPath);
            await _web.PostFileAsync(ListPath + "/Add",
                new Dictionary<string, string> { ["Name"] = name },
                fileName, content);
        }

        private static IElement RowFor(IDocument page, string frameworkName)
            => page.QuerySelectorAll("#frameworks tbody tr")
                .SingleOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName)
               ?? throw new Xunit.Sdk.XunitException($"The framework list has no row for '{frameworkName}'.");
    }
}