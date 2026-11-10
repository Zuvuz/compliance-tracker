using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-120: the Excel traceability report, downloaded through the Compliance tab's form and opened with the OpenXml SDK.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (the form #traceability-download with audience, format and sub).
    [Binding]
    public class TraceabilityReportStepDefinitions
    {
        private const string Green = "FFC6EFCE";
        private const string Red = "FFFFC7CE";
        private const string Grey = "FFEDEDED";

        private readonly WebScenario _web;
        private byte[] _file = [];

        public TraceabilityReportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I download the traceability matrix of {string} as Excel for the team")]
        public Task WhenIDownloadForTheTeam(string project) => DownloadAsync(project, "team");

        [When("I download the traceability matrix of {string} as Excel for an auditor")]
        public Task WhenIDownloadForAnAuditor(string project) => DownloadAsync(project, "auditor");

        [When("I download the traceability matrix of {string} as Excel for the management")]
        public Task WhenIDownloadForTheManagement(string project) => DownloadAsync(project, "management");

        [Then("the workbook has the sheets {string} and {string}")]
        public void ThenTheWorkbookHasTheSheets(string first, string second)
            => Assert.Equal([first, second], Read(book => SheetNames(book)));

        // US-141 R8
        [Then("the workbook has these sheets in order:")]
        public void ThenTheWorkbookHasTheseSheets(DataTable table)
            => Assert.Equal(table.Rows.Select(r => r["Sheet"]).ToList(), Read(book => SheetNames(book)));

        [Then("on {string} the first row says {string} {string} and {string} {string}")]
        public void ThenTheFirstRowSays(string sheet, string column, string value, string otherColumn, string otherValue)
        {
            var (header, rows) = Table(sheet);
            Assert.NotEmpty(rows);
            Assert.Equal(value, rows[0][header.IndexOf(column)]);
            Assert.Equal(otherValue, rows[0][header.IndexOf(otherColumn)]);
        }

        [Then("the workbook has only the sheet {string}")]
        public void ThenTheWorkbookHasOnlyTheSheet(string name)
            => Assert.Equal([name], Read(book => SheetNames(book)));

        [Then("the header of {string} is bold, frozen and filtered")]
        public void ThenTheHeaderIsBoldFrozenAndFiltered(string sheet)
            => Read(book =>
            {
                var worksheet = Worksheet(book, sheet);
                var header = worksheet.Descendants<Cell>().First();
                var format = Formats(book)[(int)header.StyleIndex!.Value];
                Assert.NotNull(Fonts(book)[(int)format.FontId!.Value].GetFirstChild<Bold>());
                Assert.Equal(PaneStateValues.Frozen, worksheet.Descendants<Pane>().Single().State!.Value);
                Assert.NotNull(worksheet.GetFirstChild<AutoFilter>());
                return true;
            });

        [Then("on {string} the status of {string} is {string} in red")]
        public void ThenTheStatusIsRed(string sheet, string code, string status) => AssertStatus(sheet, code, status, Red);

        [Then("on {string} the status of {string} is {string} in grey")]
        public void ThenTheStatusIsGrey(string sheet, string code, string status) => AssertStatus(sheet, code, status, Grey);

        [Then("on {string} the status of {string} is {string} in green")]
        public void ThenTheStatusIsGreen(string sheet, string code, string status) => AssertStatus(sheet, code, status, Green);

        [Then("the first row of {string} is a requirement that is {string}")]
        public void ThenTheFirstRowIs(string sheet, string status)
        {
            var (header, rows) = Table(sheet);
            Assert.Equal(status, rows[0][header.IndexOf("Status")]);
        }

        [Then("on {string} the row {string} lists the story {string}")]
        public void ThenTheRowListsTheStory(string sheet, string code, string story)
        {
            var (header, rows) = Table(sheet);
            Assert.Contains(rows, r => r[header.IndexOf("Code")] == code && r[header.IndexOf("Item")] == "Story" && r[header.IndexOf("Name")] == story);
        }

        [Then("on {string} the row {string} says {string} {string} and {string} {string}")]
        public void ThenTheRowSays(string sheet, string code, string column, string value, string otherColumn, string otherValue)
        {
            var (header, rows) = Table(sheet);
            var row = rows.First(r => r[header.IndexOf("Code")] == code && r[header.IndexOf("Item")] == "Story");
            Assert.Equal(value, row[header.IndexOf(column)]);
            Assert.Equal(otherValue, row[header.IndexOf(otherColumn)]);
        }

        [Then("the summary says {string} is {string}")]
        public void ThenTheSummarySays(string label, string value)
        {
            var rows = Read(book => Rows(book, "Summary"));
            var row = rows.FirstOrDefault(r => r.Count >= 2 && r[0] == label)
                ?? throw new Xunit.Sdk.XunitException($"The summary has no '{label}'.");
            Assert.Equal(value, row[1]);
        }

        [Then("the download is logged as {string}")]
        public async Task ThenTheDownloadIsLogged(string details)
        {
            await using var db = _web.CreateDbContext();
            Assert.Contains(details, await db.ActivityLog.Where(e => e.Action == ActivityAction.ReportDownloaded).Select(e => e.Details).ToListAsync());
        }

        // ---------------------------------------------------------- Helpers

        // Chooses the reader, Excel and "All regulations" in the Compliance tab's form, like a user
        private async Task DownloadAsync(string project, string audience)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/compliance");
            }

            var action = _web.Page!.QuerySelector("#traceability-download")!.GetAttribute("action")!;
            var response = await _web.Client.GetAsync($"{action}?audience={audience}&format=xlsx&sub=");
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
            _file = await response.Content.ReadAsByteArrayAsync();
        }

        private void AssertStatus(string sheet, string code, string status, string colour)
            => Read(book =>
            {
                var header = Rows(book, sheet)[0];
                var statusColumn = header.IndexOf("Status");
                var row = Worksheet(book, sheet).Descendants<Row>().Skip(1)
                    .First(r => Text(book, r.Elements<Cell>().ElementAt(header.IndexOf("Code"))) == code);
                var cell = row.Elements<Cell>().ElementAt(statusColumn);
                Assert.Equal(status, Text(book, cell));
                var fill = Fills(book)[(int)Formats(book)[(int)cell.StyleIndex!.Value].FillId!.Value];
                Assert.Equal(colour, fill.PatternFill?.ForegroundColor?.Rgb?.Value);
                return true;
            });

        private (List<string> Header, List<List<string>> Rows) Table(string sheet)
        {
            var rows = Read(book => Rows(book, sheet));
            return (rows[0], rows.Skip(1).ToList());
        }

        private T Read<T>(Func<SpreadsheetDocument, T> read)
        {
            using var stream = new MemoryStream(_file);
            using var book = SpreadsheetDocument.Open(stream, false);
            return read(book);
        }

        private static List<string> SheetNames(SpreadsheetDocument book)
            => book.WorkbookPart!.Workbook.Descendants<Sheet>().Select(s => s.Name!.Value!).ToList();

        private static Worksheet Worksheet(SpreadsheetDocument book, string name)
        {
            var sheet = book.WorkbookPart!.Workbook.Descendants<Sheet>().Single(s => s.Name == name);
            return ((WorksheetPart)book.WorkbookPart.GetPartById(sheet.Id!)).Worksheet;
        }

        private static List<List<string>> Rows(SpreadsheetDocument book, string sheet)
            => Worksheet(book, sheet).Descendants<Row>().Select(r => r.Elements<Cell>().Select(c => Text(book, c)).ToList()).ToList();

        private static string Text(SpreadsheetDocument book, Cell cell) => cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? "";

        private static List<CellFormat> Formats(SpreadsheetDocument book)
            => book.WorkbookPart!.WorkbookStylesPart!.Stylesheet.CellFormats!.Elements<CellFormat>().ToList();

        private static List<Font> Fonts(SpreadsheetDocument book)
            => book.WorkbookPart!.WorkbookStylesPart!.Stylesheet.Fonts!.Elements<Font>().ToList();

        private static List<Fill> Fills(SpreadsheetDocument book)
            => book.WorkbookPart!.WorkbookStylesPart!.Stylesheet.Fills!.Elements<Fill>().ToList();
    }
}
