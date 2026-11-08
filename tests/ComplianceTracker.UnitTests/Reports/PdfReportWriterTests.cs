using ComplianceTracker.Application.Reports;
using ComplianceTracker.Infrastructure.Reports;
using UglyToad.PdfPig;

namespace ComplianceTracker.UnitTests.Reports;

// Edge-case unit tests of US-131: the PDF text rules and the PDF writer. Arrange / Act / Assert.
public class PdfReportWriterTests
{
    private static readonly PdfHeader Header = new("Acme ApS", "Acme Portal", "Traceability", new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc), "Anna");

    private static IReadOnlyList<string> Pages(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))).ToList();
    }

    private static ReportDocument Report(int rows, int columns = 3, string cell = "text")
        => new("Traceability – Acme Portal", [("Coverage", "50 %")],
        [
            new ReportSheet("CRA", Enumerable.Range(1, columns).Select(i => $"Column {i}").ToList(), 0,
                Enumerable.Range(1, rows).Select(r => new ReportRow(Enumerable.Range(1, columns).Select(c => $"{cell} {r}.{c}").ToList(), ReportStatus.Good)).ToList())
        ]);

    // ---------------------------------------------------------- PdfText

    [Theory]
    [InlineData("Æblegrød øst Åen", "Æblegrød øst Åen")]
    [InlineData("€ – — … „quoted“", "€ – — … „quoted“")]
    [InlineData("Status: Open → Closed", "Status: Open -> Closed")]
    [InlineData("A ↔ B", "A <-> B")]
    [InlineData("Emoji 🙂 here", "Emoji ? here")]
    [InlineData("中文", "??")]
    [InlineData(null, "")]
    public void Text_is_cleaned_to_what_the_font_can_show(string? text, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, PdfText.Clean(text));
    }

    [Fact]
    public void Line_breaks_are_kept_and_control_characters_dropped()
    {
        // Act / Assert
        Assert.Equal("a\nb", PdfText.Clean("a\r\n\u0007b"));
    }

    [Fact]
    public void Wrapping_keeps_words_together()
    {
        // Act: every character is 1 wide, the line 10
        var lines = PdfText.Wrap("alpha beta gamma delta", 10, t => t.Length);

        // Assert
        Assert.Equal(["alpha beta", "gamma", "delta"], lines);
    }

    [Fact]
    public void A_word_longer_than_the_line_is_cut()
    {
        // Act
        var lines = PdfText.Wrap("abcdefghijklmnop", 5, t => t.Length);

        // Assert
        Assert.Equal(["abcde", "fghij", "klmno", "p"], lines);
        Assert.All(lines, l => Assert.True(l.Length <= 5));
    }

    [Fact]
    public void Empty_text_gives_one_empty_line()
    {
        // Act / Assert
        Assert.Equal([""], PdfText.Wrap("", 10, t => t.Length));
    }

    // ---------------------------------------------------------- The writer

    [Fact]
    public void Every_page_has_the_header_and_page_x_of_y()
    {
        // Act
        var pages = Pages(new PdfPigReportWriter().FromReport(Report(120), Header));

        // Assert
        Assert.True(pages.Count > 1);
        for (var i = 0; i < pages.Count; i++)
        {
            Assert.Contains("Acme ApS · Acme Portal · Traceability", pages[i], StringComparison.Ordinal);
            Assert.Contains("by Anna", pages[i], StringComparison.Ordinal);
            Assert.Contains($"Page {i + 1} of {pages.Count}", pages[i], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_table_over_several_pages_repeats_its_header_and_loses_no_row()
    {
        // Act
        var pages = Pages(new PdfPigReportWriter().FromReport(Report(150), Header));
        var all = string.Join(" ", pages);

        // Assert
        Assert.All(pages, p => Assert.Contains("Column 1", p, StringComparison.Ordinal));
        Assert.Contains("text 1.1", all, StringComparison.Ordinal);
        Assert.Contains("text 150.3", all, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_report_still_makes_one_page()
    {
        // Act
        var pages = Pages(new PdfPigReportWriter().FromReport(Report(0), Header));

        // Assert
        Assert.Single(pages);
        Assert.Contains("No rows", pages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_very_long_cell_is_wrapped_not_cut()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat("requirement", 200));

        // Act
        var all = string.Join(" ", Pages(new PdfPigReportWriter().FromReport(Report(1, 2, longText), Header)));

        // Assert: every word is there
        Assert.Equal(200 * 2, all.Split(' ').Count(w => w == "requirement"));
    }

    [Fact]
    public void Many_columns_are_printed_in_landscape()
    {
        // Act
        using var document = PdfDocument.Open(new PdfPigReportWriter().FromReport(Report(3, 10), Header));

        // Assert
        var page = document.GetPage(1);
        Assert.True(page.Width > page.Height);
    }

    [Fact]
    public void A_document_has_its_headings_bullets_and_parts_to_complete()
    {
        // Arrange
        var document = new GeneratedDocument("draft.docx",
        [
            new DocumentBlock(DocumentBlockKind.Title, "Technical documentation"),
            new DocumentBlock(DocumentBlockKind.Heading1, "1. General description"),
            new DocumentBlock(DocumentBlockKind.Paragraph, "Web shop for spare parts"),
            new DocumentBlock(DocumentBlockKind.Bullet, "Sign in with Keycloak"),
            new DocumentBlock(DocumentBlockKind.ToBeCompleted, "To be completed: test reports")
        ]);

        // Act
        var all = string.Join(" ", Pages(new PdfPigReportWriter().FromDocument(document, Header)));

        // Assert
        Assert.Contains("1. General description", all, StringComparison.Ordinal);
        Assert.Contains("• Sign in with Keycloak", all, StringComparison.Ordinal);
        Assert.Contains("To be completed: test reports", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Characters_the_font_cannot_show_do_not_break_the_file()
    {
        // Act
        var all = string.Join(" ", Pages(new PdfPigReportWriter().FromReport(Report(1, 1, "Open → 🙂"), Header)));

        // Assert
        Assert.Contains("Open -> ?", all, StringComparison.Ordinal);
    }
}
