using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Scanning;

namespace ComplianceTracker.UnitTests.Scanning;

// US-177: withdrawing a scan, an SBOM or test results. Edge cases. Arrange / Act / Assert.
public class WithdrawUploadTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    // ---------------------------------------------------------- Findings (R1)

    [Fact]
    public void A_finding_first_seen_in_the_withdrawn_scan_is_withdrawn()
    {
        var finding = Finding(First);

        Assert.True(finding.UndoScan(First));

        Assert.Equal(FindingState.Withdrawn, finding.State);
        Assert.False(finding.IsOpen);
    }

    [Fact]
    public void A_finding_the_withdrawn_scan_marked_as_fixed_is_open_again()
    {
        var finding = Finding(First);
        finding.MissingFrom(Second, Monday);

        finding.UndoScan(Second);

        Assert.Equal(FindingState.Open, finding.State);
        Assert.Null(finding.FixedAt);
        Assert.Equal(First, finding.LastScanId);
    }

    [Fact]
    public void A_fixed_finding_the_withdrawn_scan_brought_back_is_fixed_again()
    {
        var finding = Finding(First);
        var third = Guid.NewGuid();
        finding.MissingFrom(Second, Monday);
        finding.SeenIn(third);

        finding.UndoScan(third);

        Assert.Equal(FindingState.Fixed, finding.State);
        Assert.Equal(Monday, finding.FixedAt);
        Assert.Equal(Second, finding.LastScanId);
    }

    [Fact]
    public void A_finding_seen_in_two_scans_stays_open_when_the_second_is_withdrawn()
    {
        var finding = Finding(First);
        finding.SeenIn(Second);

        finding.UndoScan(Second);

        Assert.Equal(FindingState.Open, finding.State);
        Assert.Equal(First, finding.LastScanId);
    }

    [Fact]
    public void An_acceptance_made_after_the_scan_stays()
    {
        var finding = Finding(First);
        finding.SeenIn(Second);
        finding.Accept("Not reachable", Anna, Monday);

        finding.UndoScan(Second);

        Assert.Equal(FindingState.Accepted, finding.State);
    }

    [Fact]
    public void A_finding_the_scan_did_not_touch_is_left_alone()
    {
        var finding = Finding(First);

        Assert.False(finding.UndoScan(Second));
        Assert.Equal(FindingState.Open, finding.State);
    }

    [Fact]
    public void A_withdrawn_finding_reported_again_by_a_later_scan_is_open()
    {
        var finding = Finding(First);
        finding.UndoScan(First);

        finding.SeenIn(Second);

        Assert.Equal(FindingState.Open, finding.State);
    }

    // ---------------------------------------------------------- The reason (R4)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_reason_is_needed(string? reason)
        => Assert.Equal("Give a reason for withdrawing it", Assert.Throws<DomainException>(() => Withdrawal.Reason(reason)).Message);

    [Fact]
    public void A_reason_is_trimmed_and_at_most_500_characters()
    {
        Assert.Equal("Wrong project", Withdrawal.Reason("  Wrong project "));
        Assert.Throws<DomainException>(() => Withdrawal.Reason(new string('a', Withdrawal.MaxReasonLength + 1)));
    }

    [Fact]
    public void A_scan_cannot_be_withdrawn_twice()
    {
        var scan = Scan.Create(Guid.NewGuid(), "trivy", "scan.sarif", [1], Anna, Monday);
        scan.Withdraw("Wrong project", Anna, Monday);

        Assert.Throws<DomainException>(() => scan.Withdraw("Again", Anna, Monday));
        Assert.Equal("Wrong project", scan.WithdrawnReason);
    }

    // ---------------------------------------------------------- SBOM (R3)

    [Fact]
    public void An_sbom_is_used_by_a_report_locked_while_it_was_the_newest_of_its_version()
    {
        var first = Sbom("2.0", Monday);
        var second = Sbom("2.0", Monday.AddDays(2));
        var others = new[] { first, second };

        Assert.True(first.UsedByReportLockedAt(Monday.AddDays(1), others));
        Assert.False(first.UsedByReportLockedAt(Monday.AddDays(3), others)); // the second was newer by then
        Assert.False(second.UsedByReportLockedAt(Monday.AddDays(1), others)); // locked before it came
        Assert.True(second.UsedByReportLockedAt(Monday.AddDays(3), others));
    }

    [Fact]
    public void An_sbom_of_another_version_does_not_count()
    {
        var first = Sbom("2.0", Monday);
        var other = Sbom("3.0", Monday.AddDays(1));

        Assert.True(first.UsedByReportLockedAt(Monday.AddDays(2), [first, other]));
    }

    // ---------------------------------------------------------- Test results

    [Fact]
    public void Test_results_with_nothing_waiting_are_not_listed()
    {
        var requirement = Guid.NewGuid();
        var waiting = EvidenceSuggestion.FromTestRun(Guid.NewGuid(), requirement, "sha-a", "a.trx", ["x"], [], Monday);
        var blocked = EvidenceSuggestion.FromTestRun(Guid.NewGuid(), Guid.NewGuid(), "sha-a", "a.trx", ["x"], ["y"], Monday);
        var done = EvidenceSuggestion.FromTestRun(Guid.NewGuid(), requirement, "sha-b", "b.trx", ["x"], [], Monday.AddDays(1));
        done.Withdraw();

        var upload = Assert.Single(TestResultsUpload.From([waiting, blocked, done]));

        Assert.Equal(("sha-a", "a.trx", 2), (upload.Sha256, upload.FileName, upload.Waiting));
    }

    private static Finding Finding(Guid scanId)
        => Domain.Scanning.Finding.Create(Guid.NewGuid(), "trivy", "fp-1", "CVE-1", FindingSeverity.High, "Bad", null, scanId);

    private static Sbom Sbom(string version, DateTime at)
        => Domain.Scanning.Sbom.Create(Guid.NewGuid(), version, "CycloneDX 1.6", "sbom.json", [1], [], Anna, at);
}
