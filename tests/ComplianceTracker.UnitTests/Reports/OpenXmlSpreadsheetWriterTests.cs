using ComplianceTracker.Application.Reports;
using ComplianceTracker.Infrastructure.Reports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ComplianceTracker.UnitTests.Reports;

// Edge cases of the Excel writer (US-120): empty sheets, many columns, odd text and formulas. Arrange / Act / Assert.
public class OpenXmlSpreadsheetWriterTests
{
    private readonly OpenXmlSpreadsheetWriter _writer = new();

    private static ReportDocument Document(params ReportSheet[] sheets)
        => new("Traceability – Acme Portal", [("Project", "Acme Portal")], sheets);

    private static ReportSheet Sheet(string name, IReadOnlyList<string> columns, params ReportRow[] rows)
        => new(name, columns, 0, rows);

    private static T Open<T>(byte[] file, Func<SpreadsheetDocument, T> read)
    {
        using var stream = new MemoryStream(file);
        using var book = SpreadsheetDocument.Open(stream, false);
        return read(book);
    }

    private static List<List<string>> Rows(SpreadsheetDocument book, string sheet)
    {
        var entry = book.WorkbookPart!.Workbook.Descendants<Sheet>().Single(s => s.Name == sheet);
        var part = (WorksheetPart)book.WorkbookPart.GetPartById(entry.Id!);
        return part.Worksheet.Descendants<Row>().Select(r => r.Elements<Cell>().Select(c => c.InlineString?.InnerText ?? "").ToList()).ToList();
    }

    [Fact]
    public void A_document_without_sheets_has_only_the_summary()
    {
        // Arrange
        var document = Document();

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        Assert.Equal(["Summary"], Open(file, b => b.WorkbookPart!.Workbook.Descendants<Sheet>().Select(s => s.Name!.Value).ToList()));
    }

    [Fact]
    public void A_sheet_without_rows_still_has_its_header_and_filter()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["Code", "Status"]));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        var rows = Open(file, b => Rows(b, "CRA"));
        Assert.Equal([["Code", "Status"]], rows);
        Assert.Equal("A1:B1", Open(file, b => b.WorkbookPart!.WorksheetParts.SelectMany(p => p.Worksheet.Descendants<AutoFilter>()).Single().Reference!.Value));
    }

    [Fact]
    public void More_than_26_columns_get_names_after_Z()
    {
        // Arrange
        var columns = Enumerable.Range(1, 28).Select(i => $"C{i}").ToList();
        var document = Document(Sheet("Wide", columns, new ReportRow(columns, ReportStatus.Normal)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        var references = Open(file, b => b.WorkbookPart!.WorksheetParts.SelectMany(p => p.Worksheet.Descendants<Cell>())
            .Select(c => c.CellReference!.Value).ToList());
        Assert.Contains("Z1", references);
        Assert.Contains("AA1", references);
        Assert.Contains("AB2", references);
    }

    [Fact]
    public void A_formula_is_kept_as_text_and_never_run()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["Name"], new ReportRow(["=HYPERLINK(\"http://evil\")"], ReportStatus.Normal)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        Open(file, b =>
        {
            var cells = b.WorkbookPart!.WorksheetParts.SelectMany(p => p.Worksheet.Descendants<Cell>()).ToList();
            Assert.DoesNotContain(cells, c => c.CellFormula is not null);
            Assert.All(cells, c => Assert.Equal(CellValues.InlineString, c.DataType!.Value));
            return true;
        });
    }

    [Fact]
    public void Control_characters_are_removed_but_new_lines_are_kept()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["Name"], new ReportRow(["a\u0001b\u0007c\nd"], ReportStatus.Normal)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        Assert.Equal("abc\nd", Open(file, b => Rows(b, "CRA"))[1][0]);
    }

    [Fact]
    public void Text_longer_than_an_Excel_cell_is_cut_to_32767_characters()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["Name"], new ReportRow([new string('x', 40000)], ReportStatus.Normal)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        Assert.Equal(32767, Open(file, b => Rows(b, "CRA"))[1][0].Length);
    }

    [Fact]
    public void A_row_with_fewer_cells_than_columns_is_written()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["Code", "Status", "Name"], new ReportRow(["I.1"], ReportStatus.Bad)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        Assert.Equal(["I.1"], Open(file, b => Rows(b, "CRA"))[1]);
    }

    [Fact]
    public void Column_widths_stay_between_8_and_60()
    {
        // Arrange
        var document = Document(Sheet("CRA", ["A", "B"], new ReportRow(["x", new string('y', 500)], ReportStatus.Normal)));

        // Act
        var file = _writer.ToXlsx(document);

        // Assert
        var widths = Open(file, b => b.WorkbookPart!.WorksheetParts
            .Select(p => p.Worksheet).First(w => w.Descendants<AutoFilter>().Any())
            .Descendants<Column>().Select(c => c.Width!.Value).ToList());
        Assert.Equal([8d, 60d], widths);
    }
}
