using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;

namespace ComplianceTracker.Domain.Tests.Scanning;

// Unit tests of Scan and Finding (US-078). Every test follows Arrange / Act / Assert.
public class FindingTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Finding Create()
        => Finding.Create(Guid.NewGuid(), "Trivy", "hash:1", "CVE-2026-1111", FindingSeverity.High, "A vulnerable package", "package-lock.json:12", Guid.NewGuid());

    [Fact]
    public void A_finding_no_longer_reported_is_fixed()
    {
        // Arrange
        var finding = Create();

        // Act
        var changed = finding.MissingFrom(Guid.NewGuid(), Monday);

        // Assert
        Assert.True(changed);
        Assert.Equal(FindingState.Fixed, finding.State);
        Assert.Equal(Monday, finding.FixedAt);
    }

    [Fact]
    public void A_fixed_finding_that_comes_back_is_open_again()
    {
        // Arrange
        var finding = Create();
        finding.MissingFrom(Guid.NewGuid(), Monday);

        // Act
        finding.SeenIn(Guid.NewGuid());

        // Assert
        Assert.True(finding.IsOpen);
        Assert.Null(finding.FixedAt);
    }

    [Fact]
    public void An_accepted_finding_stays_accepted()
    {
        // Arrange
        var finding = Create();
        finding.Accept("Not reachable in our use", Guid.NewGuid(), Monday);

        // Act
        var changed = finding.MissingFrom(Guid.NewGuid(), Monday);
        finding.SeenIn(Guid.NewGuid());

        // Assert
        Assert.False(changed);
        Assert.Equal(FindingState.Accepted, finding.State);
        Assert.Equal("Not reachable in our use", finding.AcceptedReason);
    }

    [Fact]
    public void Accepting_needs_a_reason()
    {
        // Arrange
        var finding = Create();

        // Act
        var error = Assert.Throws<DomainException>(() => finding.Accept(" ", Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("A reason is required", error.Message);
    }

    [Fact]
    public void A_scan_says_what_the_import_did()
    {
        // Arrange
        var scan = Scan.Create(Guid.NewGuid(), "Trivy", "trivy.sarif", [1, 2, 3], Guid.NewGuid(), Monday);

        // Act
        scan.Record(3, 0, 3, 1);

        // Assert
        Assert.Equal("Trivy: 3 new, 0 fixed, 3 open (1 critical)", scan.Summary);
        Assert.Equal(64, scan.Sha256.Length);
    }
}
