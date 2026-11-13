using System.Text;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Infrastructure.WorkItems.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.UnitTests.WorkItems;

// US-152: edge-case unit tests of US-032 (Excel/CSV), US-033 (Word) and US-034 (Unified Process use cases).
// Arrange / Act / Assert.
public class RequirementImportTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    // A .docx with the given paragraphs; "# " makes a heading
    private static byte[] Docx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => p.StartsWith("# ", StringComparison.Ordinal)
                ? new Paragraph(new ParagraphProperties(new OutlineLevel { Val = 0 }), new Run(new Text(p[2..])))
                : new Paragraph(new Run(new Text(p) { Space = SpaceProcessingModeValues.Preserve })))));
        }

        return stream.ToArray();
    }

    // ---------------------------------------------------------- The ID pattern (US-033 R2, US-034 R2)

    [Theory]
    [InlineData("REQ-012 The user can log in", "REQ-012", "The user can log in")]
    [InlineData("  SEC-3: Encrypt at rest", "SEC-3", "Encrypt at rest")]
    [InlineData("REQ-7 – Backups", "REQ-7", "Backups")]
    [InlineData("REQ-12", "REQ-12", "")]
    public void An_id_at_the_start_gives_the_id_and_the_rest_as_the_title(string text, string id, string title)
        => Assert.Equal((id, title), IdPattern.Create(null).MatchStart(text));

    [Theory]
    [InlineData("The REQ-012 says")]  // not at the start
    [InlineData("REQ-012a extra")]     // followed by more letters
    [InlineData("R-1 too short")]      // one letter
    [InlineData("")]
    public void Text_that_does_not_start_with_an_id_is_not_a_requirement(string text)
        => Assert.Null(IdPattern.Create(null).MatchStart(text));

    [Fact]
    public void Use_cases_get_their_own_default_even_when_the_requirement_default_is_given()
    {
        Assert.Equal(IdPattern.UseCaseDefault, IdPattern.Create(null, DocumentKind.UseCases).Value);
        Assert.Equal(IdPattern.UseCaseDefault, IdPattern.Create(IdPattern.Default, DocumentKind.UseCases).Value);
        Assert.Equal(@"K\d+", IdPattern.Create(@" K\d+ ", DocumentKind.UseCases).Value);
    }

    [Fact]
    public void A_pattern_that_is_invalid_or_too_long_is_refused()
    {
        Assert.Equal("The ID pattern is not a valid regular expression", Assert.Throws<DomainException>(() => IdPattern.Create("[A-Z")).Message);
        Assert.Throws<DomainException>(() => IdPattern.Create(new string('a', IdPattern.MaxLength + 1)));
    }

    [Fact]
    public void A_pattern_that_takes_too_long_stops_after_a_second_with_a_message()
    {
        var evil = IdPattern.Create("(a+)+b");

        Assert.Equal("The ID pattern is too slow. Use a simpler pattern",
            Assert.Throws<DomainException>(() => evil.MatchStart(new string('a', 40) + "!")).Message);
    }

    // ---------------------------------------------------------- The file (US-032 R1)

    [Fact]
    public void Only_xlsx_csv_and_docx_are_accepted_and_the_message_names_them()
    {
        Assert.Equal("Only .xlsx, .csv and .docx files can be uploaded", RequirementFile.AllowedMessage);
        Assert.Equal(RequirementFile.AllowedMessage, Assert.Throws<DomainException>(() => RequirementFile.Create("spec.pdf", Utf8("%PDF-"))).Message);
    }

    [Fact]
    public void An_empty_file_or_a_docx_that_is_not_a_zip_is_refused()
    {
        Assert.Equal("The file is empty", Assert.Throws<DomainException>(() => RequirementFile.Create("spec.csv", [])).Message);
        Assert.Throws<DomainException>(() => RequirementFile.Create("spec.docx", Utf8("not a zip")));
    }

    [Fact]
    public void The_file_name_loses_its_folders_and_the_extension_its_case()
    {
        var file = RequirementFile.Create(@"C:\Users\anna\Spec.CSV", Utf8("ID;Title\nR-1;A"));

        Assert.Equal(("Spec.CSV", ".csv"), (file.FileName, file.Extension));
    }

    // ---------------------------------------------------------- CSV and the table (US-032 R2, R3)

    private static RequirementReadResult Csv(string text) => new CsvRequirementReader().Read(Utf8(text), IdPattern.Create(null));

    [Fact]
    public void Danish_headers_and_the_semicolon_are_understood()
    {
        var result = Csv("Krav-ID;Titel;Beskrivelse;Acceptkriterier\nKR-1;Log ind;Med MFA;Koden udløber");

        var item = result.Items.Single().Item;
        Assert.Equal(("KR-1", "Log ind", "Med MFA", "Koden udløber"), (item.Reference, item.Title, item.Description, item.AcceptanceCriteria));
    }

    [Fact]
    public void A_comma_file_with_quotes_line_breaks_and_doubled_quotes_is_read_as_Excel_writes_it()
    {
        var result = Csv("ID,Title,Description\r\nR-1,\"Login, with MFA\",\"Line one\nline \"\"two\"\"\"\r\nR-2,Backups,");

        Assert.Equal(["Login, with MFA", "Backups"], result.Items.Select(i => i.Item.Title));
        Assert.Equal("Line one\nline \"two\"", result.Items[0].Item.Description);
    }

    [Fact]
    public void A_byte_order_mark_does_not_hide_the_first_column()
        => Assert.Single(Csv("\uFEFFID;Title\nR-1;A").Items);

    [Fact]
    public void Rows_without_an_id_or_a_title_are_skipped_with_the_row_number_and_empty_rows_silently()
    {
        var result = Csv("ID;Title\nR-1;A\n;No id\n;;\nR-3;\nR-4;D");

        Assert.Equal(["R-1", "R-4"], result.Items.Select(i => i.Item.Reference));
        Assert.Equal(["Row 3 skipped: no ID", "Row 5 skipped: no title"], result.Skipped);
        Assert.Equal(6, result.Items[1].Position);
    }

    [Theory]
    [InlineData("Name;Text\nx;y", "The column ID is missing")]
    [InlineData("ID;Text\nx;y", "The column Title is missing")]
    [InlineData("", "The file has no rows")]
    public void A_file_without_the_needed_columns_says_which_is_missing(string text, string message)
        => Assert.Equal(message, Assert.Throws<DomainException>(() => Csv(text)).Message);

    [Fact]
    public void A_short_row_gives_empty_cells_instead_of_failing()
        => Assert.Equal("", Csv("ID;Title;Description\nR-1;A").Items.Single().Item.Description);

    // ---------------------------------------------------------- Word (US-033)

    private static RequirementReadResult Word(params string[] paragraphs) => new WordRequirementReader().Read(Docx(paragraphs), IdPattern.Create(null));

    [Fact]
    public void A_requirement_takes_the_paragraphs_until_the_next_id_or_heading_and_its_criteria_after_the_label()
    {
        var result = Word("# 2. Functional requirements", "REQ-1 Log in", "The user signs in.", "Acceptance criteria: MFA is asked", "Wrong codes are refused",
            "REQ-2 Log out", "# 3. Non-functional requirements", "This text belongs to no requirement");

        var first = result.Items[0].Item;
        Assert.Equal(("REQ-1", "Log in", "The user signs in.", "MFA is asked\nWrong codes are refused"),
            (first.Reference, first.Title, first.Description, first.AcceptanceCriteria));
        Assert.Equal(("REQ-2", ""), (result.Items[1].Item.Reference, result.Items[1].Item.Description));
        Assert.Equal("paragraph", result.PositionName);
    }

    [Fact]
    public void A_requirement_without_a_title_is_skipped_by_its_id()
        => Assert.Equal(["REQ-9 skipped: no title"], Word("REQ-9", "REQ-10 Backups").Skipped);

    [Fact]
    public void A_document_without_any_id_says_to_check_the_pattern()
        => Assert.Equal("No requirements found. Check the ID pattern",
            Assert.Throws<DomainException>(() => Word("Just some text", "and more")).Message);

    [Fact]
    public void A_file_that_is_not_a_Word_document_is_refused()
        => Assert.Throws<DomainException>(() => new WordRequirementReader().Read(Utf8("PK not really"), IdPattern.Create(null)));

    // ---------------------------------------------------------- Use cases (US-034): each starts at a heading with its ID

    private static RequirementReadResult UseCases(params string[] paragraphs)
        => new UseCaseReader().Read(Docx(paragraphs), IdPattern.Create(null, DocumentKind.UseCases));

    [Fact]
    public void A_use_case_reads_its_actor_and_numbered_main_scenario_in_English_or_Danish()
    {
        var result = UseCases("# UC-01 Register a patient", "Primær aktør: Nurse", "Hovedforløb:", "1. The nurse opens the form", "2) The system saves it",
            "Extensions: 2a. The CPR number is wrong");

        var sections = result.Items.Single().Item.UseCase!;
        Assert.Equal("Nurse", sections.PrimaryActor);
        Assert.Equal(["The nurse opens the form", "The system saves it"], sections.Steps);
    }

    [Fact]
    public void A_use_case_without_a_main_scenario_or_a_name_is_skipped_and_says_why()
    {
        var result = UseCases("# UC-01 Register", "Primary actor: Nurse", "# UC-02", "Main success scenario:", "1. Do it");

        Assert.Equal(["UC-01 skipped: no main success scenario", "UC-02 skipped: no name"], result.Skipped);
    }

    [Fact]
    public void A_document_without_use_cases_says_to_check_the_pattern()
        => Assert.Equal("No use cases found. Check the ID pattern", Assert.Throws<DomainException>(() => UseCases("# REQ-1 Not a use case", "UC-01 not a heading")).Message);

    // ---------------------------------------------------------- Which reader (US-083)

    [Theory]
    [InlineData(".XLSX", DocumentKind.RequirementSpecification, "RequirementSpecification:.xlsx")]
    [InlineData(".docx", DocumentKind.UseCases, "UseCases:.docx")]
    public void A_reader_is_found_by_its_kind_and_extension_whatever_the_case(string extension, DocumentKind kind, string key)
        => Assert.Equal(key, RequirementDocumentReaderFactory.Key(extension, kind));
}
