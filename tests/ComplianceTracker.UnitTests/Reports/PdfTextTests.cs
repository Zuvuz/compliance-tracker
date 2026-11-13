using ComplianceTracker.Application.Reports;
using ComplianceTracker.Infrastructure.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// Unit tests of the text the PDF reports can show (US-131, US-054). Every test follows Arrange / Act / Assert.
public class PdfTextTests
{
    private static readonly string Windows1252 = new string(Enumerable.Range(0x20, 0xFF - 0x20 + 1).Select(i => (char)i).ToArray())
        + "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ";

    [Fact]
    public void Every_Windows_1252_character_can_be_written_in_a_header_and_a_cell()
    {
        var writer = new PdfPigReportWriter();
        var failed = new List<char>();
        foreach (var c in Windows1252)
        {
            try
            {
                writer.FromReport(new ReportDocument($"Title {c}", [($"Label {c}", $"Value {c}")],
                    [new ReportSheet($"Sheet {c}", [$"Column {c}"], -1, [new ReportRow([$"Cell {c}"], ReportStatus.Normal)])]),
                    new PdfHeader($"Org {c}", $"Project {c}", $"Report {c}", DateTime.UtcNow, $"Anna {c}"));
            }
            catch (InvalidOperationException)
            {
                failed.Add(c);
            }
        }

        Assert.Empty(failed);
    }

    [Theory]
    [InlineData("Påmindelse", "Paamindelse")]
    [InlineData("3 × 4", "3 x 4")]
    [InlineData("Søren Ærø", "Søren Ærø")]
    [InlineData("Café", "Café")]
    [InlineData("10 €", "10 EUR")]
    public void Letters_without_a_glyph_get_a_close_form_and_the_others_are_kept(string text, string expected)
        => Assert.Equal(expected, PdfText.Clean(text));
}
