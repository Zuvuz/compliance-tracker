using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace ComplianceTracker.Specs.Support
{
    // US-032: requirement specifications for the upload tests, built with Open XML like Excel saves them
    // (text in the shared string table, cells with references such as "B3").
    public static class TestDocuments
    {
        // An .xlsx file with one sheet: the header row, then the rows. An empty cell is left out, as Excel does.
        public static byte[] Xlsx(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
        {
            using var stream = new MemoryStream();
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = document.AddWorkbookPart();
                workbook.Workbook = new Workbook();
                var strings = workbook.AddNewPart<SharedStringTablePart>();
                strings.SharedStringTable = new SharedStringTable();
                var sheetPart = workbook.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                sheetPart.Worksheet = new Worksheet(data);
                workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheetPart), SheetId = 1, Name = "Krav" }));

                var number = 0u;
                foreach (var values in new[] { header }.Concat(rows))
                {
                    number++;
                    var row = new Row { RowIndex = number };
                    for (var column = 0; column < values.Count; column++)
                    {
                        if (string.IsNullOrEmpty(values[column]))
                        {
                            continue;
                        }

                        strings.SharedStringTable.AppendChild(new SharedStringItem(new Text(values[column])));
                        var index = strings.SharedStringTable.ChildElements.Count - 1;
                        row.AppendChild(new Cell
                        {
                            CellReference = $"{(char)('A' + column)}{number}",
                            DataType = CellValues.SharedString,
                            CellValue = new CellValue(index.ToString())
                        });
                    }

                    data.AppendChild(row);
                }

                workbook.Workbook.Save();
            }

            return stream.ToArray();
        }

        // "REQ-003 Log in with MitID" → ("REQ-003", "Log in with MitID")
        public static (string Id, string Title) Requirement(string text)
        {
            var space = text.IndexOf(' ');
            return space < 0 ? (text, "") : (text[..space], text[(space + 1)..]);
        }

        // An .xlsx file with the columns ID and Title, one row per "ID Title"
        public static byte[] XlsxWith(params string[] requirements)
            => Xlsx(["ID", "Title"], requirements.Select(Requirement).Select(r => (IReadOnlyList<string>)[r.Id, r.Title]));

        // A CSV file in UTF-8, e.g. "ID;Title\nREQ-001;Log in"
        public static byte[] Csv(string text) => Encoding.UTF8.GetBytes(text);
    }
}
