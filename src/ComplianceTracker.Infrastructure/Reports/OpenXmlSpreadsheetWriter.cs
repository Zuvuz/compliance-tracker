using ComplianceTracker.Application.Reports;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-120 R3: a report as an Excel workbook with the OpenXml SDK (no Excel needed): a Summary sheet, then one sheet
    // per regulation with a bold frozen header, a filter, fitting column widths, wrapped text and the status in colour.
    // Every cell is an inline string, so nothing in the data can run as a formula (OWASP CSV injection, US-052 R6).
    public class OpenXmlSpreadsheetWriter : ISpreadsheetWriter
    {
        // The cell formats of the stylesheet below, by index
        private const uint Wrap = 1;
        private const uint Header = 2;
        private const uint Label = 3;
        private const uint Title = 4;

        private static uint StatusFormat(ReportStatus status) => status switch
        {
            ReportStatus.Good => 5,
            ReportStatus.Bad => 6,
            ReportStatus.Muted => 7,
            ReportStatus.Warning => 8,
            _ => Wrap
        };

        public byte[] ToXlsx(ReportDocument document)
        {
            using var stream = new MemoryStream();
            using (var package = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = package.AddWorkbookPart();
                workbook.Workbook = new Workbook();
                var styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = Stylesheet();

                var sheets = workbook.Workbook.AppendChild(new Sheets());
                var definedNames = new DefinedNames();
                uint id = 1;

                AddSheet(workbook, sheets, id++, "Summary", SummarySheet(document));
                foreach (var sheet in document.Sheets)
                {
                    var index = id - 1;
                    AddSheet(workbook, sheets, id++, sheet.Name, DataSheet(sheet));
                    var lastColumn = ColumnName(sheet.Columns.Count);
                    definedNames.Append(new DefinedName($"'{sheet.Name}'!$A$1:${lastColumn}${sheet.Rows.Count + 1}")
                    {
                        Name = "_xlnm._FilterDatabase",
                        LocalSheetId = index,
                        Hidden = true
                    });
                }

                if (definedNames.HasChildren)
                {
                    workbook.Workbook.Append(definedNames);
                }

                workbook.Workbook.Save();
            }

            return stream.ToArray();
        }

        private static void AddSheet(WorkbookPart workbook, Sheets sheets, uint id, string name, Worksheet worksheet)
        {
            var part = workbook.AddNewPart<WorksheetPart>();
            part.Worksheet = worksheet;
            sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = id, Name = name });
        }

        // Two columns: label and value, with the title on top
        private static Worksheet SummarySheet(ReportDocument document)
        {
            var data = new SheetData();
            data.Append(Row(1, [(document.Title, Title)]));
            uint number = 3;
            foreach (var (label, value) in document.Summary)
            {
                data.Append(Row(number++, [(label, Label), (value, Wrap)]));
            }

            return new Worksheet(
                new Columns(Width(1, 38), Width(2, 70)),
                data);
        }

        private static Worksheet DataSheet(ReportSheet sheet)
        {
            var data = new SheetData();
            data.Append(Row(1, sheet.Columns.Select(c => (c, Header)).ToList()));
            uint number = 2;
            foreach (var row in sheet.Rows)
            {
                data.Append(Row(number++, row.Cells.Select((value, i) => (value, i == sheet.StatusColumn ? StatusFormat(row.Status) : Wrap)).ToList()));
            }

            // Fitting widths: the longest text in the column, between 8 and 60 characters
            var columns = new Columns();
            for (var i = 0; i < sheet.Columns.Count; i++)
            {
                var longest = sheet.Rows.Select(r => i < r.Cells.Count ? r.Cells[i].Length : 0).Append(sheet.Columns[i].Length).Max();
                columns.Append(Width((uint)i + 1, Math.Clamp(longest + 2, 8, 60)));
            }

            var lastColumn = ColumnName(sheet.Columns.Count);
            return new Worksheet(
                new SheetViews(new SheetView(
                    new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
                    new Selection { Pane = PaneValues.BottomLeft, ActiveCell = "A2", SequenceOfReferences = new ListValue<StringValue> { InnerText = "A2" } })
                { WorkbookViewId = 0 }),
                columns,
                data,
                new AutoFilter { Reference = $"A1:{lastColumn}{sheet.Rows.Count + 1}" });
        }

        private static Row Row(uint number, IReadOnlyList<(string Value, uint Format)> cells)
        {
            var row = new Row { RowIndex = number };
            for (var i = 0; i < cells.Count; i++)
            {
                row.Append(new Cell
                {
                    CellReference = $"{ColumnName(i + 1)}{number}",
                    DataType = CellValues.InlineString,
                    StyleIndex = cells[i].Format,
                    InlineString = new InlineString(new Text(Clean(cells[i].Value)) { Space = SpaceProcessingModeValues.Preserve })
                });
            }

            return row;
        }

        private static Column Width(uint index, double width)
            => new() { Min = index, Max = index, Width = width, CustomWidth = true };

        // 1 → A, 27 → AA
        private static string ColumnName(int number)
        {
            var name = "";
            while (number > 0)
            {
                var rest = (number - 1) % 26;
                name = (char)('A' + rest) + name;
                number = (number - 1) / 26;
            }

            return name;
        }

        // XML cannot hold most control characters; Excel's cell limit is 32 767 characters
        private static string Clean(string value)
        {
            var text = new string((value ?? "").Where(c => c is '\t' or '\n' or '\r' || !char.IsControl(c)).ToArray());
            return text.Length <= 32767 ? text : text[..32767];
        }

        private static Stylesheet Stylesheet()
            => new(
                new Fonts(
                    new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
                    new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
                    new Font(new Bold(), new FontSize { Val = 14 }, new FontName { Val = "Calibri" })),
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                    Solid("FFD9E1F2"),  // 2 header: light blue
                    Solid("FFC6EFCE"),  // 3 good: green
                    Solid("FFFFC7CE"),  // 4 bad: red
                    Solid("FFEDEDED"),  // 5 muted: grey
                    Solid("FFFFEB9C")), // 6 warning: amber
                new Borders(new Border()),
                new CellFormats(
                    new CellFormat(),                                                  // 0 default
                    Format(font: 0, fill: 0),                                          // 1 wrap
                    Format(font: 1, fill: 2),                                          // 2 header
                    Format(font: 1, fill: 0),                                          // 3 label
                    new CellFormat { FontId = 2, ApplyFont = true },                   // 4 title
                    Format(font: 0, fill: 3),                                          // 5 good
                    Format(font: 0, fill: 4),                                          // 6 bad
                    Format(font: 0, fill: 5),                                          // 7 muted
                    Format(font: 0, fill: 6)));                                        // 8 warning

        private static Fill Solid(string argb)
            => new(new PatternFill(new ForegroundColor { Rgb = argb }, new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid });

        private static CellFormat Format(uint font, uint fill)
            => new(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top })
            {
                FontId = font,
                FillId = fill,
                ApplyFont = true,
                ApplyFill = fill > 0,
                ApplyAlignment = true
            };
    }
}
