using System.IO.Compression;
using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Infrastructure.Compliance;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.UnitTests.Organizations;

// US-188: edge cases of the gap analysis upload, the quote and the Word and Excel files. Arrange / Act / Assert.
public class GapAnalysisTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return memory.ToArray();
    }

    // ---------------------------------------------------------- The ZIP (R1)

    [Fact]
    public void A_zip_is_unpacked_and_its_folders_are_flattened()
    {
        var (files, problems) = GapFiles.Expand([("acme.zip", Zip(("policies/a.txt", Utf8("A")), ("b.txt", Utf8("B"))))]);

        Assert.Equal(["a.txt", "b.txt"], files.Select(f => f.Name).Order());
        Assert.Empty(problems);
    }

    [Fact]
    public void An_empty_zip_gives_nothing()
    {
        var (files, problems) = GapFiles.Expand([("empty.zip", Zip())]);

        Assert.Empty(files);
        Assert.Empty(problems);
    }

    [Fact]
    public void The_systems_own_files_and_folders_are_left_out()
    {
        var (files, _) = GapFiles.Expand([("a.zip", Zip(("__MACOSX/._a.txt", Utf8("x")), (".DS_Store", Utf8("x")), ("docs/", []), ("a.txt", Utf8("A"))))]);

        Assert.Equal(["a.txt"], files.Select(f => f.Name));
    }

    [Fact]
    public void A_zip_inside_a_zip_is_not_opened_and_says_so()
    {
        var (files, problems) = GapFiles.Expand([("outer.zip", Zip(("inner.zip", Zip(("a.txt", Utf8("A"))))))]);

        Assert.Empty(files);
        Assert.Equal("A ZIP inside a ZIP is not opened; upload it on its own", Assert.Single(problems).Note);
    }

    [Fact]
    public void A_broken_zip_is_listed_as_not_read()
    {
        var (_, problems) = GapFiles.Expand([("broken.zip", Utf8("not a zip"))]);

        Assert.Equal("The ZIP file cannot be opened", Assert.Single(problems).Note);
    }

    [Fact]
    public void A_file_in_the_zip_over_10_MB_is_not_read()
    {
        var (files, problems) = GapFiles.Expand([("big.zip", Zip(("big.txt", new byte[EvidenceFile.MaxSizeInBytes + 1])))]);

        Assert.Empty(files);
        Assert.Equal("The file is larger than 10 MB", Assert.Single(problems).Note);
    }

    [Fact]
    public void More_than_200_files_are_listed_and_not_read()
    {
        var many = Enumerable.Range(1, GapFiles.MaxFiles + 2).Select(i => ($"f{i}.txt", Utf8("x"))).ToArray();

        var (files, problems) = GapFiles.Expand(many);

        Assert.Equal(GapFiles.MaxFiles, files.Count);
        Assert.Equal(2, problems.Count);
    }

    [Theory]
    [InlineData("Access control policy.pdf", OrganisationDocumentKind.Policy)]
    [InlineData("Informationssikkerhedspolitik.docx", OrganisationDocumentKind.Policy)]
    [InlineData("Backup procedure.docx", OrganisationDocumentKind.Procedure)]
    [InlineData("Risk register.xlsx", OrganisationDocumentKind.RiskTable)]
    [InlineData("pentest-2026.pdf", OrganisationDocumentKind.Other)]
    public void The_kind_is_guessed_from_the_file_name(string name, OrganisationDocumentKind kind)
        => Assert.Equal(kind, GapFiles.Kind(name));

    [Theory]
    [InlineData("Backup_procedure.docx", "Backup procedure")]
    [InlineData(".docx", ".docx")]
    public void The_title_is_the_file_name_without_its_extension(string name, string title)
        => Assert.Equal(title, GapFiles.Title(name));

    // ---------------------------------------------------------- The quote (R2)

    [Fact]
    public void The_sentence_sharing_most_words_with_the_requirement_is_quoted()
    {
        var quote = Passages.Best("This plan is owned by IT. Incidents are reported and handled within 24 hours. Coffee is free.",
            "Incident handling: incidents are detected, reported and handled");

        Assert.Equal("Incidents are reported and handled within 24 hours.", quote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nothing here fits at all, really.")]
    public void No_text_or_no_shared_word_gives_no_quote(string? text)
        => Assert.Null(Passages.Best(text, "Incident handling"));

    [Fact]
    public void A_long_sentence_is_cut_with_an_ellipsis()
    {
        var quote = Passages.Best("Backup " + string.Join(" ", Enumerable.Repeat("backup", 200)) + ".", "backup");

        Assert.Equal(Passages.MaxLength, quote!.Length);
        Assert.EndsWith("…", quote);
    }

    // ---------------------------------------------------------- Word and Excel (R1)

    private static byte[] WordFile(string text)
    {
        using var memory = new MemoryStream();
        using (var document = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document))
        {
            document.AddMainDocumentPart().Document = new Word.Document(new Word.Body(new Word.Paragraph(new Word.Run(new Word.Text(text)))));
        }

        return memory.ToArray();
    }

    private static byte[] ExcelFile(string a1, string b1)
    {
        using var memory = new MemoryStream();
        using (var book = SpreadsheetDocument.Create(memory, SpreadsheetDocumentType.Workbook))
        {
            var workbook = book.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheet = workbook.AddNewPart<WorksheetPart>();
            sheet.Worksheet = new Worksheet(new SheetData(new Row(
                new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(a1)) },
                new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(b1)) })));
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = "Risks" }));
        }

        return memory.ToArray();
    }

    [Fact]
    public void A_word_document_is_accepted_and_its_text_is_read()
    {
        var file = EvidenceFile.Create("Backup procedure.docx", WordFile("A daily backup is copied to a second site."));

        Assert.Equal(EvidenceFile.WordContentType, file.ContentType);
        Assert.Contains("daily backup", new EvidenceTextReader(new DiagramReaders([])).Read(file.ContentType, file.Content));
    }

    [Fact]
    public void An_excel_workbook_is_read_a_row_per_line()
    {
        var file = EvidenceFile.Create("Risk register.xlsx", ExcelFile("Ransomware", "High"));

        Assert.Equal("Ransomware | High", new EvidenceTextReader(new DiagramReaders([])).Read(file.ContentType, file.Content)!.Trim());
    }

    [Fact]
    public void A_file_called_docx_that_is_not_one_is_refused()
        => Assert.Throws<DomainException>(() => EvidenceFile.Create("fake.docx", Utf8("plain text")));

    [Fact]
    public void A_broken_word_file_gives_no_text_instead_of_an_error()
        => Assert.Null(new EvidenceTextReader(new DiagramReaders([])).Read(EvidenceFile.WordContentType, [0x50, 0x4B, 0x03, 0x04, 0x00]));
}
