using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using Text = DocumentFormat.OpenXml.Spreadsheet.Text;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;

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

        // US-033: a .docx file, one paragraph per line. "# " makes the line a "heading 1", as Word's built-in style does.
        public static byte[] Docx(IEnumerable<string> lines)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                var styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = new Styles(new Style(
                    new StyleName { Val = "heading 1" },
                    new StyleParagraphProperties(new OutlineLevel { Val = 0 }))
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading1"
                });

                var body = new Body();
                foreach (var line in lines)
                {
                    var heading = line.StartsWith("# ", StringComparison.Ordinal);
                    var paragraph = new Paragraph(new Run(new WordText(heading ? line[2..] : line) { Space = SpaceProcessingModeValues.Preserve }));
                    if (heading)
                    {
                        paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" });
                    }

                    body.AppendChild(paragraph);
                }

                main.Document = new Document(body);
                main.Document.Save();
            }

            return stream.ToArray();
        }

        // A Word document from a doc string in a scenario
        public static byte[] Docx(string text) => Docx(text.Replace("\r", "").Split('\n'));

        // US-034: the tests name use case documents "use-cases...docx"
        public static bool IsUseCaseDocument(string fileName)
            => fileName.StartsWith("use-case", StringComparison.OrdinalIgnoreCase);

        // US-033: a requirement specification of the type its name says, one requirement per "ID Title".
        // In Word each requirement is a heading with a line of description under it.
        // US-034: in a use case document each use case gets a primary actor and a main success scenario of two steps.
        public static byte[] For(string fileName, params string[] requirements)
        {
            if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return XlsxWith(requirements);
            }

            if (IsUseCaseDocument(fileName))
            {
                return Docx(requirements.Select(Requirement).SelectMany(r => new[]
                {
                    $"# {r.Id} {(r.Title.Length > 0 ? r.Title : "Use case " + r.Id)}",
                    "Primary actor: Nurse",
                    "Main success scenario:",
                    "1. The nurse opens the form",
                    "2. The system saves it"
                }));
            }

            return Docx(requirements.Select(Requirement).SelectMany(r => new[]
            {
                $"# {r.Id} {(r.Title.Length > 0 ? r.Title : "Requirement " + r.Id)}",
                $"The description of {r.Id}."
            }));
        }
    }
}
