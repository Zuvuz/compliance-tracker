using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Security;

// Edge-case unit tests of US-127: security events that are not reportable, and upgrading them. Arrange / Act / Assert.
public class NotReportableSecurityEventTests
{
    private static readonly DateTime Aware = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static SecurityEvent Record(SecurityEventKind kind = SecurityEventKind.VulnerabilityNotExploited)
        => SecurityEvent.Record(Guid.NewGuid(), kind, "XSS in search", null, Aware, Anna, UserRole.Developer, Aware);

    [Theory]
    [InlineData(SecurityEventKind.VulnerabilityNotExploited)]
    [InlineData(SecurityEventKind.IncidentNotSevere)]
    public void A_not_reportable_event_has_no_deadlines(SecurityEventKind kind)
    {
        // Arrange / Act
        var securityEvent = Record(kind);

        // Assert
        Assert.False(securityEvent.IsReportable);
        Assert.All(Enum.GetValues<SecurityReportKind>(), r =>
        {
            Assert.Null(securityEvent.DueAt(r));
            Assert.Equal(SecurityReportStatus.NotRequired, securityEvent.Status(r, Aware.AddDays(30)));
        });
    }

    [Fact]
    public void A_report_cannot_be_marked_as_sent_on_a_not_reportable_event()
    {
        // Arrange
        var securityEvent = Record();

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.MarkSent(SecurityReportKind.EarlyWarning, Aware.AddHours(1), "X", Aware.AddHours(2)));
    }

    [Fact]
    public void Upgrading_moves_the_deadlines_and_keeps_the_first_awareness()
    {
        // Arrange
        var securityEvent = Record();
        var reportable = Aware.AddDays(2);

        // Act
        securityEvent.Upgrade(reportable, "  Listed in KEV  ", Anna, Aware.AddDays(3));

        // Assert
        Assert.Equal(SecurityEventKind.ActivelyExploitedVulnerability, securityEvent.Kind);
        Assert.Equal(Aware, securityEvent.FirstAwareAt);
        Assert.Equal(reportable.AddHours(24), securityEvent.DueAt(SecurityReportKind.EarlyWarning));
        Assert.Equal("Listed in KEV", securityEvent.UpgradeReason);
        Assert.Equal(Aware.AddDays(3), securityEvent.UpgradedAt);
    }

    [Fact]
    public void An_incident_is_upgraded_to_a_severe_incident()
    {
        // Arrange
        var securityEvent = Record(SecurityEventKind.IncidentNotSevere);

        // Act
        securityEvent.Upgrade(Aware, "Customer data leaked", Anna, Aware.AddHours(1));

        // Assert: the reportable time may be the exact time of awareness
        Assert.Equal(SecurityEventKind.SevereIncident, securityEvent.Kind);
    }

    [Fact]
    public void An_event_is_upgraded_only_once()
    {
        // Arrange
        var securityEvent = Record();
        securityEvent.Upgrade(Aware.AddHours(1), "Exploited", Anna, Aware.AddHours(2));

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => securityEvent.Upgrade(Aware.AddHours(1), "Again", Anna, Aware.AddHours(3)));
        Assert.Equal("The event is upgraded already", error.Message);
    }

    [Fact]
    public void A_reportable_event_cannot_be_upgraded()
    {
        // Arrange
        var securityEvent = Record(SecurityEventKind.SevereIncident);

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => securityEvent.Upgrade(Aware, "Why", Anna, Aware.AddHours(1)));
        Assert.Equal("The event is reportable already", error.Message);
    }

    [Theory]
    [InlineData(-1)] // before the awareness
    [InlineData(25)] // after now
    public void The_reportable_time_must_be_between_awareness_and_now(int hours)
    {
        // Arrange
        var securityEvent = Record();

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.Upgrade(Aware.AddHours(hours), "Exploited", Anna, Aware.AddHours(24)));
        Assert.False(securityEvent.IsReportable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Upgrading_needs_a_reason(string? reason)
    {
        // Arrange
        var securityEvent = Record();

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.Upgrade(Aware, reason, Anna, Aware.AddHours(1)));
    }

    [Fact]
    public void A_closed_event_cannot_be_upgraded()
    {
        // Arrange
        var securityEvent = Record(SecurityEventKind.IncidentNotSevere);
        securityEvent.Close(Anna, Aware.AddHours(1));

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.Upgrade(Aware, "Severe after all", Anna, Aware.AddHours(2)));
    }

    [Fact]
    public void Disclosure_comes_after_the_fix_and_not_in_the_future()
    {
        // Arrange
        var securityEvent = Record();
        var now = Aware.AddDays(5);

        // Act / Assert: no fix yet
        Assert.Throws<DomainException>(() => securityEvent.MarkDisclosed(Aware.AddDays(1), now));

        securityEvent.MarkFixAvailable(Aware.AddDays(2), now);
        Assert.Throws<DomainException>(() => securityEvent.MarkDisclosed(Aware.AddDays(1), now)); // before the fix
        Assert.Throws<DomainException>(() => securityEvent.MarkDisclosed(now.AddMinutes(1), now)); // in the future

        securityEvent.MarkDisclosed(Aware.AddDays(2), now); // at the exact time of the fix
        Assert.Equal(Aware.AddDays(2), securityEvent.DisclosedAt);
        Assert.Throws<DomainException>(() => securityEvent.MarkDisclosed(now, now)); // twice
    }

    [Fact]
    public void Only_a_vulnerability_that_is_not_exploited_is_disclosed()
    {
        // Arrange
        var securityEvent = Record(SecurityEventKind.ActivelyExploitedVulnerability);
        securityEvent.MarkFixAvailable(Aware.AddHours(1), Aware.AddHours(2));

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.MarkDisclosed(Aware.AddHours(2), Aware.AddHours(2)));
    }

    [Fact]
    public void A_vulnerability_needs_both_fix_and_disclosure_or_a_reason_to_close()
    {
        // Arrange
        var securityEvent = Record();
        securityEvent.MarkFixAvailable(Aware.AddHours(1), Aware.AddHours(2));

        // Act / Assert: only the fix is not enough, and a blank reason is no reason
        Assert.Throws<DomainException>(() => securityEvent.Close(Anna, Aware.AddHours(2)));
        Assert.Throws<DomainException>(() => securityEvent.Close(Anna, Aware.AddHours(2), "   "));

        securityEvent.Close(Anna, Aware.AddHours(2), " Component removed ");
        Assert.False(securityEvent.IsOpen);
        Assert.Equal("Component removed", securityEvent.ClosedReason);
    }

    [Fact]
    public void A_reason_that_is_too_long_is_refused()
    {
        // Arrange
        var securityEvent = Record();

        // Act / Assert
        Assert.Throws<DomainException>(() => securityEvent.Close(Anna, Aware, new string('x', SecurityEvent.MaxDescriptionLength + 1)));
        Assert.True(securityEvent.IsOpen);
    }

    [Fact]
    public void An_upgraded_event_needs_its_reports_before_closing()
    {
        // Arrange
        var securityEvent = Record();
        securityEvent.Upgrade(Aware, "Exploited", Anna, Aware.AddHours(1));

        // Act / Assert: a reason does not replace the reports
        Assert.Throws<DomainException>(() => securityEvent.Close(Anna, Aware.AddHours(2), "Not needed"));
    }
}
