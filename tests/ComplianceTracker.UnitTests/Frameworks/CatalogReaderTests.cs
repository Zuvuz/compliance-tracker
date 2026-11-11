using System.Text;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Infrastructure.Frameworks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using TableColumns = ComplianceTracker.Application.Frameworks.TableColumns;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.UnitTests.Frameworks;

// Edge-case unit tests of US-143's readers: one per format, all made-up files (R9). Arrange / Act / Assert.
public class CatalogReaderTests
{
    private static readonly PdfRequirementExtractor Reader = new();

    // ---------------------------------------------------------- PDF

    private static byte[] Pdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var y = 800.0;
            foreach (var line in lines)
            {
                page.AddText(line, 11, new PdfPoint(50, y), font);
                y -= 20;
            }
        }

        return builder.Build();
    }

    [Fact]
    public void A_scanned_pdf_with_pages_but_no_text_is_refused()
    {
        // Arrange
        var file = CatalogFile.Create("scan.pdf", Pdf([], []));

        // Act
        var error = Assert.Throws<DomainException>(() => Reader.Extract(file));

        // Assert
        Assert.Equal(IRequirementExtractor.NoText, error.Message);
    }

    [Fact]
    public void A_pdf_that_cannot_be_read_is_empty_as_before()
    {
        // Arrange: the PDF signature and nothing else
        var bytes = new byte[200];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);

        // Act
        var result = Reader.Extract(CatalogFile.Create("broken.pdf", bytes));

        // Assert
        Assert.Empty(result.Requirements);
    }

    [Fact]
    public void A_pdf_with_only_page_numbers_counts_as_without_text()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Reader.Extract(CatalogFile.Create("scan.pdf", Pdf(["1"], ["2"]))));
    }

    // ---------------------------------------------------------- Word

    private static byte[] Word(Action<Word.Body, MainDocumentPart> fill)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Word.Document(new Word.Body());
            fill(main.Document.Body!, main);
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static Word.Paragraph Paragraph(string text, string? style = null, bool numbered = false)
    {
        var properties = new Word.ParagraphProperties();
        if (style is not null)
        {
            properties.Append(new Word.ParagraphStyleId { Val = style });
        }

        if (numbered)
        {
            properties.Append(new Word.NumberingProperties(new Word.NumberingId { Val = 1 }));
        }

        return new Word.Paragraph(properties, new Word.Run(new Word.Text(text)));
    }

    [Fact]
    public void Word_headings_numbered_by_word_get_their_number_back()
    {
        // Arrange
        var content = Word((body, _) => body.Append(
            Paragraph("Context", "Heading1", numbered: true),
            Paragraph("Understanding", "Heading2", numbered: true),
            Paragraph("The organization shall look."),
            Paragraph("Parties", "Heading2", numbered: true),
            Paragraph("Parties shall be listed."),
            Paragraph("Leadership", "Heading1", numbered: true),
            Paragraph("Commitment", "Heading2", numbered: true),
            Paragraph("Leaders shall commit.")));

        // Act
        var result = Reader.Extract(CatalogFile.Create("standard.docx", content));

        // Assert
        Assert.Equal(["1.1", "1.2", "2.1"], result.Requirements.Select(r => r.Code));
    }

    [Fact]
    public void Word_tables_give_code_title_and_text_and_skip_empty_rows()
    {
        // Arrange
        Word.TableRow Row(params string[] cells) => new(cells.Select(c => new Word.TableCell(new Word.Paragraph(new Word.Run(new Word.Text(c))))));
        var content = Word((body, _) => body.Append(
            Paragraph("Annex A (normative)"),
            Paragraph("Controls"),
            Paragraph("These shall be compared."),
            new Word.Table(Row("A.5.1", "Policies", "Policies shall be set."), Row("", "", ""), Row("A.5.2", "Roles", "Roles shall be given."))));

        // Act
        var result = Reader.Extract(CatalogFile.Create("annex.docx", content));

        // Assert
        Assert.Equal(["Annex A", "A.5.1", "A.5.2"], result.Requirements.Select(r => r.Code));
        Assert.Equal(("Policies", "Policies shall be set.", "Annex A"), (result.Requirements[1].Title, result.Requirements[1].Description, result.Requirements[1].ParentCode));
    }

    // ---------------------------------------------------------- HTML

    [Fact]
    public void Saved_html_is_read_by_its_blocks_and_scripts_and_navigation_are_ignored()
    {
        // Arrange: the shape of a page saved from an online browsing platform
        const string html = """
            <!DOCTYPE html><html><head><script>alert("x")</script><style>p{}</style></head><body>
            <nav><a>5.1 Not a requirement shall</a></nav>
            <div class="sts-section"><h2><span class="sts-label">5.1</span> Policies</h2>
              <div class="sts-p"><p>Policies <b>shall</b> be set.</p></div></div>
            <div class="sts-section"><h2>5.2 Roles</h2><p>Roles should be given.</p>
              <table><tr><td>a)</td><td>first</td></tr></table></div>
            <footer>© ISO 2022</footer></body></html>
            """;

        // Act
        var result = Reader.Extract(CatalogFile.Create("obp.html", Encoding.UTF8.GetBytes(html)));

        // Assert
        Assert.Equal(["5.1", "5.2", "5.2 a)"], result.Requirements.Select(r => r.Code));
        Assert.Equal("Policies shall be set.", result.Requirements[0].Description);
        Assert.Equal(RequirementKind.Recommendation, result.Requirements[1].Kind);
    }

    // ---------------------------------------------------------- CSV and Excel

    [Fact]
    public void A_csv_with_semicolons_quotes_and_line_breaks_is_read()
    {
        // Arrange
        var csv = "﻿Control;Name;Description\r\nA.5.1;\"Policies; and rules\";\"Line one\nline \"\"two\"\"\"\r\n;;\r\nA.5.2;Roles;Text";

        // Act
        var rows = PdfRequirementExtractor.ReadCsv(Encoding.UTF8.GetBytes(csv));

        // Assert
        Assert.Equal(["Control", "Name", "Description"], rows[0]);
        Assert.Equal(["A.5.1", "Policies; and rules", "Line one\nline \"two\""], rows[1]);
        Assert.Equal(4, rows.Count);
    }

    [Theory]
    [InlineData("Code,Title,Text\nA.1,One,Text")]
    [InlineData("Code\tTitle\tText\nA.1\tOne\tText")]
    [InlineData("\"Code,x\";Title;Text\nA.1;One;Text")]
    public void The_csv_separator_is_found_from_the_header(string csv)
    {
        // Act
        var rows = PdfRequirementExtractor.ReadCsv(Encoding.UTF8.GetBytes(csv));

        // Assert
        Assert.Equal(3, rows[0].Count);
        Assert.Equal("One", rows[1][1]);
    }

    [Fact]
    public void A_csv_without_columns_gives_nothing_until_they_are_chosen()
    {
        // Arrange
        var file = CatalogFile.Create("soa.csv", Encoding.UTF8.GetBytes("Control;Name;Description\nA.5.1;Policies;Policies shall be set."));

        // Act
        var before = Reader.Extract(file);
        var after = Reader.Extract(file, new TableColumns("Control", "Name", "Description"));

        // Assert
        Assert.Empty(before.Requirements);
        Assert.Equal(["Control", "Name", "Description"], Reader.Headers(file));
        Assert.Equal("A.5.1", Assert.Single(after.Requirements).Code);
    }

    [Fact]
    public void Excel_reads_shared_strings_numbers_and_skipped_cells_in_the_first_sheet()
    {
        // Arrange: B2 is missing, so "Text" must still land in the third column
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var strings = workbook.AddNewPart<SharedStringTablePart>();
            strings.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("Control")), new SharedStringItem(new Text("Name")));
            var sheet = workbook.AddNewPart<WorksheetPart>();
            sheet.Worksheet = new Worksheet(new SheetData(
                new Row(new Cell { CellReference = "A1", DataType = CellValues.SharedString, CellValue = new CellValue("0") },
                        new Cell { CellReference = "B1", DataType = CellValues.SharedString, CellValue = new CellValue("1") },
                        new Cell { CellReference = "C1", DataType = CellValues.InlineString, InlineString = new InlineString(new Text("Description")) }) { RowIndex = 1 },
                new Row(new Cell { CellReference = "A2", CellValue = new CellValue("12") },
                        new Cell { CellReference = "C2", DataType = CellValues.InlineString, InlineString = new InlineString(new Text("Text")) }) { RowIndex = 2 }));
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = "SoA" }));
            workbook.Workbook.Save();
        }

        var file = CatalogFile.Create("soa.xlsx", stream.ToArray());

        // Act
        var result = Reader.Extract(file, new TableColumns("Control", "Name", "Description"));

        // Assert
        var requirement = Assert.Single(result.Requirements);
        Assert.Equal(("12", "12", "Text"), (requirement.Code, requirement.Title, requirement.Description));
    }

    [Fact]
    public void Headers_are_empty_for_a_pdf()
    {
        // Act / Assert
        Assert.Empty(Reader.Headers(CatalogFile.Create("a.pdf", Pdf(["5.1 Policies"]))));
    }
}
