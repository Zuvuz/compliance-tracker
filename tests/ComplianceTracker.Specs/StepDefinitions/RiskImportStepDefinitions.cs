using System.Text;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-049: the risk import, through the web app like a user.
    // UI contract: Views/Risks/Import.cshtml (#upload-risks, #risk-import-errors li, #risk-import-preview rows
    // .import-row[data-title] with .import-owner and .import-note, form #confirm-import); Views/Risks/Details.cshtml
    // (#risk-imported).
    [Binding]
    public class RiskImportStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskImportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I import the risks file {string} into {string} with the rows")]
        [When("I import the risks file {string} into {string} with the rows")]
        public Task WhenIImportSemicolons(string fileName, string project, DataTable rows)
            => UploadAsync(project, fileName, Csv(rows, ';'));

        [When("I import the comma-separated risks file {string} into {string} with the rows")]
        public Task WhenIImportCommas(string fileName, string project, DataTable rows)
            => UploadAsync(project, fileName, Csv(rows, ','));

        [When("I import the Excel risks file {string} into {string} with the rows")]
        public Task WhenIImportExcel(string fileName, string project, DataTable rows)
            => UploadAsync(project, fileName, TestDocuments.Xlsx(
                rows.Header.ToList(), rows.Rows.Select(r => (IReadOnlyList<string>)rows.Header.Select(h => r[h]).ToList())));

        [When("I confirm the import")]
        public Task WhenIConfirm() => ConfirmAsync(importAnyway: false);

        [When("I confirm the import and import the duplicates anyway")]
        public Task WhenIConfirmAnyway() => ConfirmAsync(importAnyway: true);

        [When("{string} opens the risk import of {string}")]
        public async Task WhenOpensTheRiskImport(string userName, string project)
        {
            var id = await ProjectIdAsync(project);
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            _web.Response = await client.GetAsync($"/Risks/Import?projectId={id}");
        }

        [Then("the preview says {string}")]
        public void ThenThePreviewSays(string error)
            => Assert.Contains(error, _web.Page!.QuerySelectorAll("#risk-import-errors li").Select(li => li.TextContent.Trim()));

        [Then("the import cannot be confirmed")]
        public void ThenTheImportCannotBeConfirmed() => Assert.Null(_web.Page!.QuerySelector("#confirm-import"));

        [Then("the preview marks {string} as {string}")]
        public void ThenThePreviewMarks(string title, string note)
            => Assert.Equal(note, Row(title).QuerySelector(".import-note")!.TextContent.Trim());

        [Then("the preview shows the owner of {string} as {string}")]
        public void ThenThePreviewShowsTheOwner(string title, string owner)
            => Assert.Equal(owner, Row(title).QuerySelector(".import-owner")!.TextContent.Trim());

        [Then("{string} has no risks")]
        public async Task ThenHasNoRisks(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await ProjectIdAsync(project);
            Assert.Equal(0, await db.Risks.CountAsync(r => r.ProjectId == id));
        }

        [Then("{string} has {int} risk named {string}")]
        [Then("{string} has {int} risks named {string}")]
        public async Task ThenHasRisksNamed(string project, int count, string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await ProjectIdAsync(project);
            Assert.Equal(count, await db.Risks.CountAsync(r => r.ProjectId == id && r.Title == title));
        }

        [Then("the risk {string} says it was imported from {string} with the owner {string}")]
        public async Task ThenTheRiskSaysItWasImported(string title, string fileName, string owner)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
            var text = _web.Page!.QuerySelector("#risk-imported")?.TextContent.Trim() ?? "";
            Assert.Contains($"Imported from {fileName}", text);
            Assert.Contains($"owner {owner}", text);
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string project, string fileName, byte[] content)
        {
            await _web.OpenAsync($"/Risks/Import?projectId={await ProjectIdAsync(project)}");
            var form = _web.Page!.QuerySelector("#upload-risks")
                ?? throw new Xunit.Sdk.XunitException("The import page has no upload.");
            await _web.PostFileAsync(form.GetAttribute("action")!, Hidden(form), fileName, content);
        }

        private async Task ConfirmAsync(bool importAnyway)
        {
            var form = _web.Page!.QuerySelector("#confirm-import")
                ?? throw new Xunit.Sdk.XunitException("The preview cannot be confirmed.");
            var fields = Hidden(form);
            if (importAnyway)
            {
                fields["importAnyway"] = "true";
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private IElement Row(string title)
            => _web.Page!.QuerySelectorAll("#risk-import-preview tr.import-row").FirstOrDefault(r => r.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The preview has no row '{title}'.");

        // The table's columns as a header row, then one line per row; a cell with the separator is quoted
        private static byte[] Csv(DataTable rows, char separator)
        {
            string Quote(string cell) => cell.Contains(separator) || cell.Contains('"') ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
            var lines = new[] { string.Join(separator, rows.Header.Select(Quote)) }
                .Concat(rows.Rows.Select(r => string.Join(separator, rows.Header.Select(h => Quote(r[h])))));
            return Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        // PostFileAsync and PostFormAsync add the form token themselves
        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
