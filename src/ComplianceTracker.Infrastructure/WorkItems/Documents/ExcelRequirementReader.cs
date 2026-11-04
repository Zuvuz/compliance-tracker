using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Domain.Common;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-032: reads the first sheet of an .xlsx file with Open XML (DocumentFormat.OpenXml, MIT licence).
    // Only the cells' values are read; formulas give their last calculated value, and nothing is ever run.
    public class ExcelRequirementReader : IRequirementDocumentReader
    {
        // ASVS V5.2.3: a 10 MB file can unpack to far more; a part larger than this is refused instead of read
        internal static readonly OpenSettings Limits = new() { MaxCharactersInPart = 50_000_000 };

        // The ID column says what the ID is
        public DocumentKind Kind => DocumentKind.RequirementSpecification;

        public bool UsesIdPattern => false;

        public string Extension => ".xlsx";

        public RequirementReadResult Read(byte[] content, IdPattern idPattern)
        {
            try
            {
                using var stream = new MemoryStream(content, writable: false);
                using var document = SpreadsheetDocument.Open(stream, false, Limits);
                return RequirementTable.Read(Rows(document).ToList());
            }
            catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or IOException or FormatException
                                              or System.Xml.XmlException or InvalidOperationException)
            {
                throw new DomainException("The file could not be read as an Excel workbook");
            }
        }

        // The first sheet's rows, each with its row number and its cells in column order (gaps filled with "")
        private static IEnumerable<(int Number, IReadOnlyList<string> Cells)> Rows(SpreadsheetDocument document)
        {
            var workbook = document.WorkbookPart ?? throw new DomainException("The workbook has no sheets");
            var first = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
                ?? throw new DomainException("The workbook has no sheets");
            var sheet = (WorksheetPart)workbook.GetPartById(first.Id!.Value!);
            var strings = workbook.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(i => i.InnerText).ToList() ?? [];

            var position = 0;
            foreach (var row in sheet.Worksheet.Descendants<Row>())
            {
                position++;
                var number = (int?)row.RowIndex?.Value ?? position;
                position = number;

                var cells = new List<string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = cell.CellReference?.Value is { } reference ? ColumnIndex(reference) : cells.Count;
                    while (cells.Count < column)
                    {
                        cells.Add("");
                    }

                    cells.Add(Value(cell, strings));
                }

                yield return (number, cells);
            }
        }

        private static string Value(Cell cell, List<string> strings)
        {
            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? "";
            }

            var value = cell.CellValue?.Text ?? "";
            if (cell.DataType?.Value == CellValues.SharedString
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0 && index < strings.Count)
            {
                return strings[index];
            }

            return value;
        }

        // "C7" → 2 (A = 0)
        private static int ColumnIndex(string reference)
        {
            var index = 0;
            foreach (var c in reference.TakeWhile(char.IsLetter))
            {
                index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }

            return Math.Max(index - 1, 0);
        }
    }
}
