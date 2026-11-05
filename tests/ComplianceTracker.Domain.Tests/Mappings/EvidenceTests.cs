using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of Evidence and EvidenceFile (US-057): link or file, file checks, validity and removal.
// Every test follows Arrange / Act / Assert.
public class EvidenceTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Requirement = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static byte[] Pdf(long size = 100)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return bytes;
    }

    private static Evidence Link(string url = "https://github.com/acme/portal/pull/42", DateOnly? validUntil = null)
        => Evidence.ForLink(Project, Requirement, "PR 42", url, validUntil, Anna, UserRole.Developer, At);

    // ---------------------------------------------------------- Link (R1)

    [Fact]
    public void A_link_keeps_who_added_it_and_their_role()
    {
        // Act
        var evidence = Link();

        // Assert
        Assert.Equal("https://github.com/acme/portal/pull/42", evidence.Url);
        Assert.False(evidence.IsFile);
        Assert.Equal(Anna, evidence.AddedById);
        Assert.Equal(UserRole.Developer, evidence.AddedRole);
        Assert.True(evidence.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("github.com/acme")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    public void A_link_must_be_http_or_https(string url)
    {
        // Act and Assert
        Assert.Throws<DomainException>(() => Link(url));
    }

    [Fact]
    public void A_title_is_required()
    {
        // Act and Assert
        var error = Assert.Throws<DomainException>(() =>
            Evidence.ForLink(Project, Requirement, "  ", "https://example.com", null, Anna, null, At));
        Assert.Equal("Give the evidence a title", error.Message);
    }

    // ---------------------------------------------------------- File (R2)

    [Fact]
    public void A_file_keeps_its_name_type_size_and_sha256()
    {
        // Arrange
        var content = Pdf();

        // Act
        var file = EvidenceFile.Create("reports/pentest.pdf", content);
        var evidence = Evidence.ForFile(Project, Requirement, "Pentest", file, null, Anna, UserRole.Admin, At);

        // Assert
        Assert.Equal("pentest.pdf", evidence.FileName);
        Assert.Equal("application/pdf", evidence.ContentType);
        Assert.Equal(100, evidence.SizeInBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), evidence.Sha256);
        Assert.Null(evidence.Url);
    }

    [Fact]
    public void A_file_of_exactly_10_MB_is_accepted()
    {
        // Act
        var file = EvidenceFile.Create("big.pdf", Pdf(EvidenceFile.MaxSizeInBytes));

        // Assert
        Assert.Equal(EvidenceFile.MaxSizeInBytes, file.Content.Length);
    }

    [Fact]
    public void A_file_over_10_MB_is_refused()
    {
        // Act and Assert
        var error = Assert.Throws<DomainException>(() => EvidenceFile.Create("big.pdf", Pdf(EvidenceFile.MaxSizeInBytes + 1)));
        Assert.Equal("The file is larger than 10 MB", error.Message);
    }

    [Theory]
    [InlineData("script.exe")]
    [InlineData("page.html")]
    [InlineData("notes")]
    public void Unknown_file_types_are_refused(string name)
    {
        // Act and Assert
        Assert.Throws<DomainException>(() => EvidenceFile.Create(name, Encoding.UTF8.GetBytes("hello")));
    }

    [Fact]
    public void A_file_whose_content_does_not_match_its_extension_is_refused()
    {
        // Act and Assert: text renamed to .png
        Assert.Throws<DomainException>(() => EvidenceFile.Create("shot.png", Encoding.UTF8.GetBytes("not a picture")));
    }

    [Fact]
    public void A_binary_file_cannot_pass_as_text()
    {
        // Act and Assert
        Assert.Throws<DomainException>(() => EvidenceFile.Create("notes.txt", [0x00, 0x01, 0x02]));
    }

    [Fact]
    public void Text_files_are_accepted()
    {
        // Act
        var file = EvidenceFile.Create("results.csv", Encoding.UTF8.GetBytes("test;result\nlogin;passed"));

        // Assert
        Assert.Equal("text/csv", file.ContentType);
    }

    // ---------------------------------------------------------- Valid until (R3)

    [Fact]
    public void Evidence_is_valid_through_its_last_day()
    {
        // Arrange
        var evidence = Link(validUntil: new DateOnly(2026, 11, 3));

        // Act and Assert
        Assert.False(evidence.IsExpiredOn(new DateOnly(2026, 11, 3)));
        Assert.True(evidence.IsExpiredOn(new DateOnly(2026, 11, 4)));
    }

    [Fact]
    public void Evidence_without_an_end_date_never_expires()
    {
        // Act and Assert
        Assert.False(Link().IsExpiredOn(new DateOnly(2099, 1, 1)));
    }

    // ---------------------------------------------------------- Removing (R5)

    [Fact]
    public void Removed_evidence_is_kept_with_who_and_when()
    {
        // Arrange
        var evidence = Link();

        // Act
        evidence.Remove(Anna, At.AddHours(1));

        // Assert
        Assert.False(evidence.IsActive);
        Assert.Equal(Anna, evidence.RemovedById);
        Assert.Equal(At.AddHours(1), evidence.RemovedAt);
    }

    [Fact]
    public void Evidence_cannot_be_removed_twice()
    {
        // Arrange
        var evidence = Link();
        evidence.Remove(Anna, At);

        // Act and Assert
        Assert.Throws<DomainException>(() => evidence.Remove(Anna, At));
    }
}
