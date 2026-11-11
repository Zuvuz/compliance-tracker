using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-143: standards uploaded in every form, through the Frameworks page like a user, and checked in the database.
    // UI contract: Views/Frameworks/Index.cshtml (the upload form), Views/Frameworks/Version.cshtml (#choose-columns),
    // Views/Projects/_ComplianceTab.cshtml (requirement-row with the badge "recommended", #coverage).
    [Binding]
    public class StandardsInEveryFormStepDefinitions
    {
        private const string ListPath = "/Frameworks";
        private readonly WebScenario _web;
        private string? _versionId;

        public StandardsInEveryFormStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I upload the framework {string} from a PDF with clauses 4 to 10 and the controls A.5.1 to A.8.34")]
        public Task WhenIUploadTheManagementSystemPdf(string name) => UploadAsync(name, "iso-27001.pdf", StandardFiles.ManagementSystemPdf());

        [When("I upload the framework {string} from a Word file")]
        public Task WhenIUploadFromAWordFile(string name) => UploadAsync(name, "iso-25010.docx", StandardFiles.QualityModelWord());

        [When("I upload the framework {string} from an Excel file with the columns {string}, {string} and {string}")]
        public Task WhenIUploadFromAnExcelFile(string name, string _, string __, string ___)
            => UploadAsync(name, "soa.xlsx", StandardFiles.ControlListExcel());

        [When("I upload the framework {string} from a PDF without text")]
        public Task WhenIUploadFromAPdfWithoutText(string name) => UploadAsync(name, "scan.pdf", StandardFiles.ScannedPdf());

        // Chooses the columns in the draft page's form, like a user
        [When("I choose {string} as the code, {string} as the title and {string} as the text")]
        public async Task WhenIChooseTheColumns(string code, string title, string text)
        {
            var form = _web.Page!.QuerySelector("form#choose-columns")
                ?? throw new Xunit.Sdk.XunitException("The draft page does not offer to choose the columns.");
            foreach (var (name, value) in new[] { ("code", code), ("title", title), ("text", text) })
            {
                Assert.Contains(form.QuerySelectorAll($"select[name={name}] option"), o => o.GetAttribute("value") == value);
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["versionId"] = _versionId!,
                ["code"] = code,
                ["title"] = title,
                ["text"] = text,
                ["parent"] = "",
                ["kind"] = ""
            });
        }

        [Given("the framework {string} from a PDF where {string} says \"should\" is active")]
        public async Task GivenTheCodeOfPracticeIsActive(string name, string _)
        {
            await UploadAsync(name, "iso-27002.pdf", StandardFiles.CodeOfPracticePdf());
            await FrameworkUi.ActivateAsync(_web, _versionId!);
        }

        [When("I open the Compliance tab of {string}")]
        public async Task WhenIOpenTheComplianceTab(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/compliance");
        }

        // ---------------------------------------------------------- Then

        [Then("the draft has the requirement {string} under {string}")]
        public async Task ThenTheDraftHasTheRequirementUnder(string code, string parent)
        {
            var requirements = await DraftAsync();
            var child = requirements.SingleOrDefault(r => r.Code == code) ?? throw new Xunit.Sdk.XunitException($"The draft has no '{code}'.");
            Assert.Equal(parent, requirements.SingleOrDefault(r => r.Id == child.ParentId)?.Code);
        }

        [Then("the draft has {int} requirements under {string}")]
        public async Task ThenTheDraftHasRequirementsUnder(int count, string parent)
        {
            var requirements = await DraftAsync();
            var parentId = requirements.Single(r => r.Code == parent).Id;
            Assert.Equal(count, requirements.Count(r => r.ParentId == parentId));
        }

        [Then("the draft has {int} requirements")]
        public async Task ThenTheDraftHasRequirements(int count) => Assert.Equal(count, (await DraftAsync()).Count);

        [Then("no requirement contains {string}")]
        public async Task ThenNoRequirementContains(string text)
            => Assert.DoesNotContain(await DraftAsync(), r => r.Title.Contains(text) || r.Description.Contains(text));

        [Then("the draft has no requirement {string}")]
        public async Task ThenTheDraftHasNoRequirement(string code) => Assert.DoesNotContain(await DraftAsync(), r => r.Code == code);

        [Then("the draft has the characteristic {string} under {string}")]
        public async Task ThenTheDraftHasTheCharacteristicUnder(string title, string parent)
        {
            var requirements = await DraftAsync();
            var child = requirements.SingleOrDefault(r => r.Title == title) ?? throw new Xunit.Sdk.XunitException($"The draft has no '{title}'.");
            Assert.Equal(RequirementKind.Characteristic, child.Kind);
            var up = requirements.Single(r => r.Id == child.ParentId);
            Assert.Equal((parent, RequirementKind.Characteristic), (up.Title, up.Kind));
        }

        [Then("the requirement {string} shows {string}")]
        public void ThenTheRequirementShows(string code, string badge)
        {
            var row = _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
                ?? throw new Xunit.Sdk.XunitException($"The Compliance tab has no row '{code}'.");
            Assert.Equal(badge, row.QuerySelector(".recommended")?.TextContent.Trim());
        }

        // R7: the next version from Excel needs no choice; the remembered columns read it at once
        [Then("the next version of {string} from Excel is read with the same columns")]
        public async Task ThenTheNextVersionIsReadWithTheSameColumns(string name)
        {
            await _web.OpenAsync(ListPath);
            var row = _web.Page!.QuerySelectorAll("#frameworks tbody tr").Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == name);
            await _web.PostFileAsync(ListPath + "/UploadVersion",
                new Dictionary<string, string> { ["FrameworkId"] = row.GetAttribute("data-framework-id")! }, "soa-v2.xlsx", StandardFiles.ControlListExcel());
            _versionId = FrameworkUi.OpenVersionId(_web);
            Assert.Equal(93, (await DraftAsync()).Count);
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string name, string fileName, byte[] content)
        {
            await _web.OpenAsync(ListPath);
            await _web.PostFileAsync(ListPath + "/Add", new Dictionary<string, string> { ["Name"] = name }, fileName, content);
            _versionId = _web.CurrentPath.StartsWith("/Frameworks/Version/", StringComparison.OrdinalIgnoreCase) ? FrameworkUi.OpenVersionId(_web) : null;
        }

        private async Task<List<Requirement>> DraftAsync()
        {
            Assert.NotNull(_versionId);
            await using var db = _web.CreateDbContext();
            var id = Guid.Parse(_versionId!);
            return await db.Requirements.Where(r => r.FrameworkVersionId == id).OrderBy(r => r.Position).ToListAsync();
        }
    }
}
