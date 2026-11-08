using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Reports;

// Edge-case unit tests of US-050: a locked report and its fingerprint. Arrange / Act / Assert.
public class ReportSnapshotTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 report");

    private static ReportSnapshot Lock(byte[]? content = null, string title = "Traceability – Acme Portal (Team)", string kind = "traceability:team:all")
        => ReportSnapshot.Lock(Guid.NewGuid(), Guid.NewGuid(), kind, title, "report.pdf", "application/pdf", content ?? Pdf, Guid.NewGuid(), UserRole.Admin, Monday);

    [Fact]
    public void A_locked_report_keeps_who_when_and_the_SHA256_of_the_file()
    {
        // Act
        var snapshot = Lock();

        // Assert
        Assert.Equal(64, snapshot.Sha256.Length);
        Assert.Equal(snapshot.Sha256, snapshot.Sha256.ToLowerInvariant());
        Assert.Equal(Pdf.Length, snapshot.SizeInBytes);
        Assert.Equal(UserRole.Admin, snapshot.LockedRole);
        Assert.Equal(Monday, snapshot.LockedAt);
    }

    [Fact]
    public void The_fingerprint_is_the_known_SHA256()
    {
        // Act / Assert: SHA-256 of "abc"
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ReportSnapshot.Fingerprint(Encoding.ASCII.GetBytes("abc")));
    }

    [Fact]
    public void The_same_file_matches_and_one_changed_byte_does_not()
    {
        // Arrange
        var snapshot = Lock();
        var changed = Pdf.ToArray();
        changed[^1] ^= 0x01;

        // Act / Assert
        Assert.True(snapshot.Matches(Pdf.ToArray()));
        Assert.False(snapshot.Matches(changed));
        Assert.False(snapshot.Matches([]));
    }

    [Fact]
    public void An_empty_report_cannot_be_locked()
    {
        // Act / Assert
        Assert.Equal("The report is empty", Assert.Throws<DomainException>(() => Lock([])).Message);
    }

    [Theory]
    [InlineData(" ", "traceability:team:all")]
    [InlineData("Traceability", " ")]
    public void A_report_needs_a_title_and_a_kind(string title, string kind)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Lock(title: title, kind: kind));
    }

    [Fact]
    public void A_long_title_is_cut()
    {
        // Act
        var snapshot = Lock(title: new string('t', 400));

        // Assert
        Assert.Equal(ReportSnapshot.MaxTitleLength, snapshot.Title.Length);
    }

    [Fact]
    public void Locking_the_same_file_twice_makes_two_snapshots_with_the_same_fingerprint()
    {
        // Act
        var first = Lock();
        var second = Lock();

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Sha256, second.Sha256);
    }
}
