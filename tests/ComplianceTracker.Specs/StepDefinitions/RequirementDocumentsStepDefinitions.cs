using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-032: requirement specifications uploaded on the User stories tab, through the web app with test sign-in.
    // UI contract: Views/Projects/_RequirementDocuments.cshtml (#upload-requirement-document, .requirement-document[data-name],
    // .document-version[data-version] a) and _WorkItems.cshtml (.work-item[data-reference][data-source], .work-item-state).
    [Binding]
    public class RequirementDocumentsStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;

        // What each upload sent, so a download can be compared: (file name, version) → bytes
        private readonly Dictionary<(string FileName, int Version), byte[]> _uploaded = new();
        private string? _lastFileName;
        private byte[]? _lastContent;
        private string? _lastRequirement;

        public RequirementDocumentsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I upload the requirement specification {string} to {string} with these rows:")]
        public Task WhenIUploadWithTheseRows(string fileName, string projectName, DataTable table)
        {
            var header = table.Header.ToList();
            var rows = table.Rows.Select(r => (IReadOnlyList<string>)header.Select(h => r[h]).ToList());
            return UploadAsync(projectName, fileName, TestDocuments.Xlsx(header, rows));
        }

        [When("I upload the CSV file {string} with the separator {string} and the requirement {string}")]
        public Task WhenIUploadTheCsvFile(string fileName, string separator, string requirement)
            => UploadAsync(Project, fileName, TestDocuments.Csv($"ID{separator}Title\n{requirement}\n"));

        [Given("{string} has version 1 of {string} with {string}")]
        [When("I upload the requirement specification {string} to {string} with the requirement {string}")]
        public Task GivenHasVersion1With(string first, string second, string requirement)
        {
            // The Given names the project first, the When names the file first
            var (projectName, fileName) = first.Contains('.') ? (second, first) : (first, second);
            return UploadAsync(projectName, fileName, TestDocuments.For(fileName, requirement));
        }

        [Given("{string} has version 1 of {string} with {string} and {string}")]
        public Task GivenHasVersion1WithTwo(string projectName, string fileName, string first, string second)
            => UploadAsync(projectName, fileName, TestDocuments.For(fileName, first, second));

        [When("I upload version 2 of {string} with only {string}")]
        public Task WhenIUploadVersion2Of(string fileName, string requirement)
            => UploadAsync(Project, fileName, TestDocuments.For(fileName, requirement));

        [When("I upload version 2 with {string} and {string}")]
        public Task WhenIUploadVersion2With(string first, string second)
            => UploadAsync(Project, _lastFileName!, TestDocuments.For(_lastFileName!, first, second));

        [When("I upload version 2 with only {string}")]
        public Task WhenIUploadVersion2WithOnly(string requirement)
            => UploadAsync(Project, _lastFileName!, TestDocuments.For(_lastFileName!, requirement));

        // US-033: a Word document from the doc string, "# " for a heading
        [When("I upload the Word document {string} to {string} containing:")]
        public Task WhenIUploadTheWordDocument(string fileName, string projectName, string text)
            => UploadAsync(projectName, fileName, TestDocuments.Docx(text));

        [When("I upload the Word document {string} with the heading {string}")]
        public Task WhenIUploadTheWordDocumentWithTheHeading(string fileName, string heading)
        {
            _lastContent = TestDocuments.Docx([$"# {heading}", "The description."]);
            return UploadAsync(Project, fileName, _lastContent);
        }

        // US-033 R6: the same file again, with another ID pattern
        [When("I upload it again with the ID pattern {string}")]
        public Task WhenIUploadItAgainWithTheIdPattern(string pattern)
            => UploadAsync(Project, _lastFileName!, _lastContent!, pattern);

        // R4: the header is row 1, so the ID is put on the given rows and other IDs everywhere else
        [When("I upload a requirement specification where {string} is in rows {int} and {int}")]
        public Task WhenIUploadWithADuplicate(string id, int firstRow, int secondRow)
        {
            var rows = Enumerable.Range(2, secondRow - 1)
                .Select(row => (IReadOnlyList<string>)[row == firstRow || row == secondRow ? id : $"REQ-1{row:00}", $"Requirement {row}"]);
            return UploadAsync(Project, "kravspec.xlsx", TestDocuments.Xlsx(["ID", "Title"], rows));
        }

        // R1: a file of another type, with content that matches its name
        [When("I upload the requirement specification {string} to {string}")]
        public Task WhenIUploadTheFile(string fileName, string projectName)
        {
            var content = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => TestFiles.Pdf(),
                ".xlsx" => TestDocuments.XlsxWith("REQ-001 Log in"),
                ".docx" => TestDocuments.For(fileName, "REQ-001 Log in"),
                _ => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0] // an old binary Office file
            };
            return UploadAsync(projectName, fileName, content);
        }

        [When("{string} downloads version {int} of {string}")]
        public async Task WhenDownloadsVersion(string userName, int version, string fileName)
        {
            var path = await DownloadPathAsync(fileName, version);
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync(path);
        }

        // ---------------------------------------------------------- Then

        [Then("the project has the requirements {string} and {string}")]
        public async Task ThenTheProjectHasTheRequirements(string first, string second)
        {
            await OpenAsync(Project);
            Requirement(first);
            Requirement(second);
        }

        [Then("the project has the requirement {string}")]
        public async Task ThenTheProjectHasTheRequirement(string id)
        {
            await OpenAsync(Project);
            Assert.Equal("Requirement", Requirement(id).QuerySelector(".work-item-type")?.TextContent.Trim());
        }

        // US-033 R2, R3
        [Then("the project has the requirement {string} with the title {string}")]
        public async Task ThenTheProjectHasTheRequirementWithTheTitle(string id, string title)
        {
            await OpenAsync(Project);
            Assert.Equal(title, Requirement(id).QuerySelector(".work-item-title")?.TextContent.Trim());
            _lastRequirement = id;
        }

        [Then("its acceptance criteria are {string}")]
        public void ThenItsAcceptanceCriteriaAre(string criteria)
            => Assert.Equal(criteria, Requirement(_lastRequirement!).QuerySelector(".work-item-acceptance")?.TextContent.Trim());

        // US-033 R4: an ID inside the text is not a requirement
        [Then("the project has no requirement {string}")]
        public async Task ThenTheProjectHasNoRequirement(string id)
        {
            await OpenAsync(Project);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll(".work-item[data-source=Document]"), w => w.GetAttribute("data-reference") == id);
        }

        [Then("the requirement {string} is still active")]
        public Task ThenTheRequirementIsStillActive(string id)
            => ThenTheRequirementIsMarked(id, "Active");

        [Then("the requirement {string} is marked {string}")]
        public async Task ThenTheRequirementIsMarked(string id, string state)
        {
            await OpenAsync(Project);
            Assert.Equal(state, Requirement(id).QuerySelector(".work-item-state")?.TextContent.Trim());
        }

        [Then("the project has no requirements")]
        public async Task ThenTheProjectHasNoRequirements()
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.WorkItems.AnyAsync(w => w.Source == Domain.WorkItems.WorkItemSource.Document));
            Assert.False(await db.RequirementDocuments.AnyAsync());
        }

        // R7: the stored file is the one that was uploaded, byte for byte
        [Then("version {int} can still be downloaded")]
        public async Task ThenVersionCanStillBeDownloaded(int version)
        {
            var response = await _web.Client.GetAsync(await DownloadPathAsync(_lastFileName!, version));
            Assert.True(response.IsSuccessStatusCode, $"The download gave {response.StatusCode}.");
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(_uploaded[(_lastFileName!, version)], await response.Content.ReadAsByteArrayAsync());
        }

        [Then("the activity log for {string} has {string} for {string}")]
        public async Task ThenTheActivityLogHasFor(string organisationName, string action, string userName)
        {
            await using var db = _web.CreateDbContext();
            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var user = organisation.Users.Single(u => u.Name == userName);
            var expected = Enum.Parse<ActivityAction>(action);

            Assert.True(await db.ActivityLog.AnyAsync(e => e.OrganizationId == organisation.Id && e.UserId == user.Id && e.Action == expected),
                $"No {action} entry for {userName}.");
        }

        // ---------------------------------------------------------- Helpers

        // Uploads through the form on the User stories tab, as a new specification or a new version of the same name
        // idPattern: what to type in the ID pattern field (US-033); null keeps the form's default
        private async Task UploadAsync(string projectName, string fileName, byte[] content, string? idPattern = null)
        {
            await OpenAsync(projectName);
            var form = _web.Page!.QuerySelector("form#upload-requirement-document")
                ?? throw new Xunit.Sdk.XunitException("The User stories tab has no upload form for requirement specifications.");

            // PostFileAsync adds the antiforgery token itself
            var fields = form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["documentId"] = "";
            fields["idPattern"] = idPattern ?? form.QuerySelector("input[name=idPattern]")?.GetAttribute("value") ?? "";

            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, content);

            // Remember what each new version held, read back from the page the upload ended on
            var document = _web.Page!.QuerySelectorAll(".requirement-document")
                .FirstOrDefault(d => string.Equals(d.GetAttribute("data-name"), fileName, StringComparison.OrdinalIgnoreCase));
            var newest = document?.QuerySelectorAll(".document-version").Select(v => int.Parse(v.GetAttribute("data-version")!)).DefaultIfEmpty(0).Max() ?? 0;
            if (newest > 0 && !_uploaded.ContainsKey((fileName, newest)))
            {
                _uploaded[(fileName, newest)] = content;
            }

            _lastFileName = fileName;
        }

        private async Task OpenAsync(string projectName)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}/user-stories");

        private IElement Requirement(string id)
            => _web.Page!.QuerySelectorAll(".work-item[data-source=Document]").FirstOrDefault(w => w.GetAttribute("data-reference") == id)
               ?? throw new Xunit.Sdk.XunitException($"The project page shows no requirement '{id}'.");

        private async Task<string> DownloadPathAsync(string fileName, int version)
        {
            await OpenAsync(Project);
            return _web.Page!.QuerySelectorAll(".requirement-document")
                       .First(d => d.GetAttribute("data-name") == fileName)
                       .QuerySelector($".document-version[data-version='{version}'] a")?.GetAttribute("href")
                   ?? throw new Xunit.Sdk.XunitException($"Version {version} of {fileName} has no download link.");
        }

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
