using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Security;

// Unit tests of SecurityEvent (US-073): the deadlines of CRA Art. 14 and the reports. Every test follows Arrange / Act / Assert.
public class SecurityEventTests
{
    private static readonly DateTime Aware = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static SecurityEvent Vulnerability(DateTime? now = null)
        => SecurityEvent.Record(Guid.NewGuid(), SecurityEventKind.ActivelyExploitedVulnerability, "CVE-2026-1234", null,
            Aware, Anna, UserRole.Developer, now ?? Aware);

    private static SecurityEvent Incident()
        => SecurityEvent.Record(Guid.NewGuid(), SecurityEventKind.SevereIncident, "Ransomware", null, Aware, Anna, null, Aware);

    [Fact]
    public void Early_warning_and_notification_count_from_when_the_manufacturer_became_aware()
    {
        // Arrange / Act
        var vulnerability = Vulnerability();

        // Assert: Art. 14(2)(a), (b)
        Assert.Equal(Aware.AddHours(24), vulnerability.DueAt(SecurityReportKind.EarlyWarning));
        Assert.Equal(Aware.AddHours(72), vulnerability.DueAt(SecurityReportKind.Notification));
        Assert.Null(vulnerability.DueAt(SecurityReportKind.FinalReport));
    }

    [Fact]
    public void A_vulnerability_final_report_is_due_14_days_after_the_fix()
    {
        // Arrange
        var vulnerability = Vulnerability(Aware.AddDays(3));

        // Act
        vulnerability.MarkFixAvailable(Aware.AddDays(2), Aware.AddDays(3));

        // Assert: Art. 14(2)(c)
        Assert.Equal(Aware.AddDays(16), vulnerability.DueAt(SecurityReportKind.FinalReport));
    }

    [Fact]
    public void An_incident_final_report_is_due_a_month_after_the_notification()
    {
        // Arrange
        var incident = Incident();

        // Act
        incident.MarkSent(SecurityReportKind.Notification, Aware.AddHours(30), "ENISA 4711", Aware.AddHours(31));

        // Assert: Art. 14(4)(c)
        Assert.Equal(Aware.AddHours(30).AddMonths(1), incident.DueAt(SecurityReportKind.FinalReport));
    }

    [Theory]
    [InlineData(SecurityReportKind.Notification, 1, SecurityReportStatus.Upcoming)] // 71 hours left
    [InlineData(SecurityReportKind.EarlyWarning, 5, SecurityReportStatus.DueSoon)] // 19 hours left
    [InlineData(SecurityReportKind.EarlyWarning, 25, SecurityReportStatus.Missed)]
    public void The_status_follows_the_clock(SecurityReportKind report, int hoursAfterAware, SecurityReportStatus expected)
    {
        // Arrange
        var vulnerability = Vulnerability();

        // Act
        var status = vulnerability.Status(report, Aware.AddHours(hoursAfterAware));

        // Assert
        Assert.Equal(expected, status);
    }

    [Fact]
    public void A_report_sent_after_its_deadline_is_late()
    {
        // Arrange
        var vulnerability = Vulnerability();

        // Act
        vulnerability.MarkSent(SecurityReportKind.EarlyWarning, Aware.AddHours(30), "ENISA 1", Aware.AddHours(31));

        // Assert
        Assert.Equal(SecurityReportStatus.SentLate, vulnerability.Status(SecurityReportKind.EarlyWarning, Aware.AddHours(31)));
    }

    [Fact]
    public void A_report_needs_a_reference_and_is_sent_once()
    {
        // Arrange
        var vulnerability = Vulnerability();

        // Act / Assert
        Assert.Throws<DomainException>(() => vulnerability.MarkSent(SecurityReportKind.EarlyWarning, Aware, " ", Aware.AddHours(1)));
        vulnerability.MarkSent(SecurityReportKind.EarlyWarning, Aware, "ENISA 1", Aware.AddHours(1));
        Assert.Throws<DomainException>(() => vulnerability.MarkSent(SecurityReportKind.EarlyWarning, Aware, "ENISA 2", Aware.AddHours(1)));
    }

    [Fact]
    public void An_event_closes_only_when_every_report_is_sent()
    {
        // Arrange
        var vulnerability = Vulnerability(Aware.AddDays(10));
        vulnerability.MarkSent(SecurityReportKind.EarlyWarning, Aware.AddHours(2), "1", Aware.AddDays(10));
        vulnerability.MarkSent(SecurityReportKind.Notification, Aware.AddHours(20), "2", Aware.AddDays(10));

        // Act / Assert
        Assert.Throws<DomainException>(() => vulnerability.Close(Anna, Aware.AddDays(10)));
        vulnerability.MarkFixAvailable(Aware.AddDays(5), Aware.AddDays(10));
        vulnerability.MarkSent(SecurityReportKind.FinalReport, Aware.AddDays(9), "3", Aware.AddDays(10));
        vulnerability.Close(Anna, Aware.AddDays(10));
        Assert.False(vulnerability.IsOpen);
    }

    [Fact]
    public void Only_a_vulnerability_has_a_fix()
    {
        // Arrange
        var incident = Incident();

        // Act / Assert
        Assert.Throws<DomainException>(() => incident.MarkFixAvailable(Aware.AddHours(1), Aware.AddHours(2)));
    }

    [Fact]
    public void Becoming_aware_cannot_be_in_the_future()
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => SecurityEvent.Record(Guid.NewGuid(), SecurityEventKind.SevereIncident, "X", null,
            Aware.AddHours(1), Anna, null, Aware));
    }
}
