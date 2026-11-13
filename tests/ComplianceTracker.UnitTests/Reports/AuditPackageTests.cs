using System.IO.Compression;
using System.Text;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.UnitTests.Reports;

// Unit tests of the audit package (US-076). Every test follows Arrange / Act / Assert.
public class AuditPackageTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static PackagePart Part(string path, string? text, string? notIncluded = null)
        => new(path, text is null ? null : Encoding.UTF8.GetBytes(text), $"Part {path}", notIncluded);

    // ---------------------------------------------------------- The period (R2)

    [Fact]
    public void Without_dates_the_period_is_the_last_12_months()
        => Assert.Equal((new DateOnly(2025, 11, 12), Today), AuditPackageService.Period(null, null, Today));

    [Fact]
    public void A_start_alone_reaches_12_months_but_not_past_today()
    {
        Assert.Equal(new DateOnly(2025, 6, 1), AuditPackageService.Period(new DateOnly(2024, 6, 1), null, Today).To);
        Assert.Equal(Today, AuditPackageService.Period(new DateOnly(2026, 6, 1), null, Today).To);
    }

    [Fact]
    public void A_period_that_ends_before_it_starts_is_refused()
        => Assert.Throws<DomainException>(() => AuditPackageService.Period(new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), Today));

    // ---------------------------------------------------------- The ZIP (R1, R3)

    private static Dictionary<string, string> Unzip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void Parts_that_are_not_included_are_left_out_of_the_zip()
        => Assert.Equal(["a.txt"], Unzip(AuditPackageService.Zip([Part("a.txt", "A"), Part("b.pdf", null, "Not made")])).Keys);

    [Fact]
    public void Two_files_with_the_same_name_are_both_kept()
    {
        var files = Unzip(AuditPackageService.Zip([Part("evidence/x.pdf", "1"), Part("evidence/x.pdf", "2")]));

        Assert.Equal(["evidence/x.pdf", "evidence/2-x.pdf"], files.Keys);
        Assert.Equal(["1", "2"], files.Values);
    }

    [Fact]
    public void The_same_parts_give_the_same_package()
        => Assert.Equal(AuditPackageService.Zip([Part("a.txt", "A"), Part("b.txt", "B")]), AuditPackageService.Zip([Part("a.txt", "A"), Part("b.txt", "B")]));

    // ---------------------------------------------------------- The contents page (R1, R3, R6)

    [Fact]
    public void The_contents_list_each_file_with_its_SHA_256_and_what_is_missing()
    {
        var included = Part("evidence/mfa.pdf", "content");

        var contents = AuditPackageService.Contents("Audit package", "Acme ApS", "Acme Portal", ["CRA – Regulation (EU) 2024/2847"],
            new DateOnly(2025, 11, 12), Today, "Anna", new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc),
            [included, Part("03-soa.pdf", null, "No ISO/IEC 27001")], ["Pentest: https://example.com/pentest"]);

        Assert.Contains($"  evidence/mfa.pdf\n      Part evidence/mfa.pdf\n      SHA-256 {included.Sha256}", contents.Replace("\r\n", "\n"));
        Assert.Contains("Not included\n  Part 03-soa.pdf: No ISO/IEC 27001", contents.Replace("\r\n", "\n"));
        Assert.Contains("Evidence that is a link\n  Pentest: https://example.com/pentest", contents.Replace("\r\n", "\n"));
        Assert.Contains("Period:       12-11-2025 – 12-11-2026", contents);
        Assert.Contains("Made:         2026-11-12T09:00:00Z by Anna", contents);
    }

    [Fact]
    public void Without_frameworks_or_missing_parts_the_contents_say_so_or_leave_it_out()
    {
        var contents = AuditPackageService.Contents("Audit package", "Acme ApS", "Acme Portal", [], Today, Today, "Anna", DateTime.UtcNow, [Part("a.txt", "A")], []);

        Assert.Contains("Frameworks:   None", contents);
        Assert.DoesNotContain("Not included", contents);
        Assert.DoesNotContain("Evidence that is a link", contents);
    }

    // ---------------------------------------------------------- File names

    [Theory]
    [InlineData("MFA config", "MFA-config")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("ISO/IEC 27001:2022", "ISO-IEC-27001-2022")]
    [InlineData("  ", "file")]
    [InlineData("2.1.0", "2.1.0")]
    public void A_name_part_has_no_folders_or_odd_characters(string text, string expected)
        => Assert.Equal(expected, AuditPackageService.Safe(text));

    [Fact]
    public void A_long_name_part_is_cut()
        => Assert.Equal(60, AuditPackageService.Safe(new string('x', 100)).Length);
}
