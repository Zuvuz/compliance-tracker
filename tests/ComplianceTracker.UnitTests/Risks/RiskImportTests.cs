using System.Text;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Infrastructure.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// Unit tests of the risk import (US-049): the rules for each row, the CSV and Excel readers, and the import itself.
// Every test follows Arrange / Act / Assert.
public class RiskImportTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<User> Members()
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        acme.AddMember(new UserIdentity("dan-sub", "Dan", "dan@acme.test"), UserRole.Developer);
        return acme.Members;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Sheet(params string[] lines)
        => lines.Select(l => (IReadOnlyList<string>)l.Split(';')).ToList();

    private static RiskImportRow One(params string[] lines)
        => RiskImportRows.Read(Sheet(lines), Members(), [], Today).Single();

    private const string Header = "Title;Description;Likelihood;Impact;Owner;Treatment;Reason;Last reviewed";

    // ---------------------------------------------------------- The file (R1, R2, R8)

    [Fact]
    public void A_file_without_the_needed_columns_is_refused()
        => Assert.Equal(["The file needs the columns Title, Likelihood and Impact in its first row"],
            RiskImportRows.FileErrors(Sheet("Name;Score", "Phishing;4")));

    [Fact]
    public void An_empty_file_and_a_header_only_file_are_refused()
    {
        Assert.NotEmpty(RiskImportRows.FileErrors([]));
        Assert.Equal(["The file has no risks"], RiskImportRows.FileErrors(Sheet(Header, ";;;;;;;")));
    }

    [Fact]
    public void More_than_500_rows_are_refused()
    {
        var lines = new[] { "Title;Likelihood;Impact" }.Concat(Enumerable.Range(1, 501).Select(i => $"Risk {i};1;1")).ToArray();

        Assert.Equal(["The file has 501 rows; at most 500 can be imported"], RiskImportRows.FileErrors(Sheet(lines)));
    }

    [Fact]
    public void Column_names_ignore_case_spaces_order_and_a_byte_order_mark()
    {
        var row = One("\uFEFFimpact;LAST REVIEWED;title;likelihood", "4;2026-05-01;Phishing;2");

        Assert.Equal(("Phishing", 2, 4, new DateOnly(2026, 5, 1)), (row.Title, row.Likelihood, row.Impact, row.LastReviewed));
    }

    // ---------------------------------------------------------- Each row (R2–R6)

    [Fact]
    public void A_full_row_is_read()
    {
        var row = One(Header, "Stolen session cookie;Session cookie stolen via XSS;3;4;Anna;Reduce;;2026-05-01");

        Assert.True(row.IsValid);
        Assert.Equal(RiskLevel.High, row.Level);
        Assert.Equal("Anna", row.Owner!.Name);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("three")]
    [InlineData("2.5")]
    [InlineData("")]
    public void Likelihood_must_be_1_to_5(string likelihood)
        => Assert.Equal(["Row 1: Likelihood must be 1 to 5"], One("Title;Likelihood;Impact", $"Phishing;{likelihood};3").Errors);

    [Fact]
    public void Rows_are_numbered_after_the_header_and_blank_rows_are_skipped()
    {
        var rows = RiskImportRows.Read(Sheet("Title;Likelihood;Impact", "A;1;1", ";;", "B;1;9"), Members(), [], Today);

        Assert.Equal(["Row 2: Impact must be 1 to 5"], rows.SelectMany(r => r.Errors));
    }

    [Fact]
    public void A_title_is_needed_and_at_most_200_characters()
    {
        Assert.Equal(["Row 1: Title is required"], One("Title;Likelihood;Impact", ";1;1").Errors);
        Assert.Equal(["Row 1: Title must be 200 characters or fewer"], One("Title;Likelihood;Impact", $"{new string('x', 201)};1;1").Errors);
    }

    [Fact]
    public void The_same_title_twice_in_the_file_is_an_error()
    {
        var rows = RiskImportRows.Read(Sheet("Title;Likelihood;Impact", "Phishing;1;1", "phishing;2;2"), Members(), [], Today);

        Assert.Equal(["Row 2: the title is also in row 1"], rows.SelectMany(r => r.Errors));
    }

    [Theory]
    [InlineData("reduce", RiskTreatment.Reduce)]
    [InlineData("TRANSFER", RiskTreatment.Transfer)]
    [InlineData("", RiskTreatment.Reduce)]
    public void Treatment_ignores_case_and_defaults_to_Reduce(string text, RiskTreatment expected)
        => Assert.Equal(expected, One("Title;Likelihood;Impact;Treatment", $"Phishing;1;1;{text}").Treatment);

    [Theory]
    [InlineData("Ignore")]
    [InlineData("7")]
    public void An_unknown_treatment_is_an_error(string text)
        => Assert.Equal(["Row 1: Treatment must be Reduce, Accept, Avoid or Transfer"], One("Title;Likelihood;Impact;Treatment", $"Phishing;1;1;{text}").Errors);

    [Fact]
    public void Accept_needs_a_reason()
        => Assert.Equal(["Row 1: Accept needs a reason"], One("Title;Likelihood;Impact;Treatment;Reason", "Phishing;1;1;Accept; ").Errors);

    [Theory]
    [InlineData("2026-05-01")]
    [InlineData("01-05-2026")]
    [InlineData("1.5.2026")]
    [InlineData("01/05/2026")]
    [InlineData("46143")] // Excel's day number for 1 May 2026
    public void Last_reviewed_is_read_in_the_usual_forms(string text)
        => Assert.Equal(new DateOnly(2026, 5, 1), One("Title;Likelihood;Impact;Last reviewed", $"Phishing;1;1;{text}").LastReviewed);

    [Fact]
    public void Last_reviewed_must_be_a_date_in_the_past()
    {
        Assert.Equal(["Row 1: Last reviewed must be a date, e.g. 2026-05-01"], One("Title;Likelihood;Impact;Last reviewed", "Phishing;1;1;last spring").Errors);
        Assert.Equal(["Row 1: Last reviewed cannot be in the future"], One("Title;Likelihood;Impact;Last reviewed", "Phishing;1;1;2026-12-01").Errors);
    }

    [Fact]
    public void An_owner_is_found_by_name_or_e_mail_and_otherwise_kept_as_text()
    {
        var rows = RiskImportRows.Read(Sheet("Title;Likelihood;Impact;Owner", "A;1;1;dan", "B;1;1;ANNA@acme.test", "C;1;1;Carl", "D;1;1;"),
            Members(), [], Today);

        Assert.Equal(["Dan", "Anna", null, null], rows.Select(r => r.Owner?.Name));
        Assert.Equal([true, true, false, true], rows.Select(r => r.OwnerIsMember));
    }

    [Fact]
    public void A_title_already_in_the_project_is_a_duplicate_not_an_error()
    {
        var row = RiskImportRows.Read(Sheet("Title;Likelihood;Impact", " phishing ;1;1"), Members(), ["Phishing"], Today).Single();

        Assert.True(row.IsDuplicate);
        Assert.True(row.IsValid);
    }

    [Fact]
    public void Several_errors_in_one_row_are_all_shown()
        => Assert.Equal(4, One("Title;Likelihood;Impact;Treatment", ";9;0;Accept").Errors.Count); // title, likelihood, impact, and Accept without a reason

    // ---------------------------------------------------------- CSV (R9)

    [Theory]
    [InlineData("Title;Likelihood;Impact", ';')]
    [InlineData("Title,Likelihood,Impact", ',')]
    [InlineData("\"Title, risk\";Likelihood;Impact", ';')]
    [InlineData("Title", ';')]
    public void The_separator_is_the_one_the_header_uses(string header, char expected)
        => Assert.Equal(expected, CsvRiskSheetReader.Separator(header + "\nA,B;C"));

    [Fact]
    public void Quoted_cells_may_hold_separators_quotes_and_line_breaks()
    {
        var rows = CsvRiskSheetReader.Parse("Title;Description\r\n\"A;B\";\"Said \"\"no\"\"\nnext line\"\r\n", ';');

        Assert.Equal(["A;B", "Said \"no\"\nnext line"], rows[1]);
    }

    [Fact]
    public void A_last_line_without_a_line_break_is_read()
        => Assert.Equal(2, CsvRiskSheetReader.Parse("Title\nPhishing", ';').Count);

    [Fact]
    public void A_file_from_Excel_in_Windows_1252_keeps_the_Danish_letters()
    {
        var content = Encoding.Latin1.GetBytes("Title;Likelihood;Impact\nFejl i løsning;1;1\n");

        Assert.Equal("Fejl i løsning", new CsvRiskSheetReader().Read(content)[1][0]);
    }

    [Fact]
    public void A_binary_file_with_a_csv_name_is_refused()
        => Assert.Throws<DomainException>(() => new CsvRiskSheetReader().Read([0x50, 0x00, 0x01]));

    [Fact]
    public void Each_reader_knows_its_own_files()
    {
        Assert.True(new CsvRiskSheetReader().CanRead("risks.CSV", []));
        Assert.False(new CsvRiskSheetReader().CanRead("risks.xlsx", []));
        Assert.False(new XlsxRiskSheetReader().CanRead("risks.xlsx", Encoding.UTF8.GetBytes("not a zip")));
    }

    [Fact]
    public void A_broken_workbook_gives_a_message()
        => Assert.Equal("The workbook cannot be read",
            Assert.Throws<DomainException>(() => new XlsxRiskSheetReader().Read([(byte)'P', (byte)'K', 3, 4, 0, 0, 0])).Message);

    // ---------------------------------------------------------- The import (R7, R8)

    [Fact]
    public void An_import_is_at_most_1_MB_and_not_empty()
    {
        Assert.Equal("The file is empty", Assert.Throws<DomainException>(() => RiskImport.Create(Guid.NewGuid(), "a.csv", [], Guid.NewGuid(), Monday)).Message);
        Assert.Equal("The file is larger than 1 MB",
            Assert.Throws<DomainException>(() => RiskImport.Create(Guid.NewGuid(), "a.csv", new byte[RiskImport.MaxSizeInBytes + 1], Guid.NewGuid(), Monday)).Message);
    }

    [Fact]
    public void An_import_keeps_only_the_file_name_and_is_confirmed_once()
    {
        var import = RiskImport.Create(Guid.NewGuid(), @"C:\Users\anna\risks.csv", [1], Guid.NewGuid(), Monday);

        import.Confirm(3, Monday);

        Assert.Equal(("risks.csv", 3, true), (import.FileName, import.ImportedCount, import.IsConfirmed));
        Assert.Throws<DomainException>(() => import.Confirm(3, Monday));
    }

    [Fact]
    public void A_risk_remembers_the_file_and_an_owner_who_is_not_a_member()
    {
        var risk = Risk.Create(Guid.NewGuid(), "Phishing", null, 2, 2, Guid.NewGuid(), RiskTreatment.Reduce, Guid.NewGuid(), Monday);

        risk.FromImport("risks.csv", "  Carl ");

        Assert.Equal(("risks.csv", "Carl"), (risk.ImportedFrom, risk.ImportedOwner));
    }
}
