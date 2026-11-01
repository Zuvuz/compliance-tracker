using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Unit tests of the day a framework version starts to apply (US-035).
// Every test follows Arrange / Act / Assert.
public class StartDateTests
{
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly DateTime January = new(2027, 1, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly StartDate = new(2027, 2, 1);

    private static CatalogFile PdfFile(byte marker)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        bytes[^1] = marker;
        return CatalogFile.Create("policy.pdf", bytes);
    }

    // Version 1 active since January; version 2 activated in January, applying from 1 February
    private static (Framework Framework, FrameworkVersion First, FrameworkVersion Second) PolicyWithUpcomingVersion()
    {
        var framework = Framework.Create(Guid.NewGuid(), "Security policy", PdfFile(1), AnnaId, January);
        var first = framework.LatestVersion;
        first.AddRequirement("1.1", "Passwords", null);
        first.Activate(AnnaId, January);

        var second = framework.AddVersion(PdfFile(2), AnnaId, January);
        second.AddRequirement("1.1", "Passwords and MFA", null);
        second.SetAppliesFrom(StartDate);
        second.Activate(AnnaId, January);
        return (framework, first, second);
    }

    [Fact]
    public void Before_the_date_the_previous_version_stays_current()
    {
        // Arrange
        var (framework, first, second) = PolicyWithUpcomingVersion();

        // Act
        var current = framework.CurrentVersion(StartDate.AddDays(-1));

        // Assert
        Assert.Same(first, current);
        Assert.Same(second, framework.UpcomingVersion(StartDate.AddDays(-1)));
        Assert.True(second.IsUpcomingOn(StartDate.AddDays(-1)));
    }

    [Fact]
    public void From_the_date_the_new_version_is_current()
    {
        // Arrange
        var (framework, _, second) = PolicyWithUpcomingVersion();

        // Act
        var current = framework.CurrentVersion(StartDate);

        // Assert
        Assert.Same(second, current);
        Assert.Null(framework.UpcomingVersion(StartDate));
    }

    [Fact]
    public void Without_a_date_a_version_applies_when_it_is_activated()
    {
        // Arrange
        var framework = Framework.Create(Guid.NewGuid(), "Security policy", PdfFile(1), AnnaId, January);
        var version = framework.LatestVersion;
        version.AddRequirement("1.1", "Passwords", null);

        // Act
        version.Activate(AnnaId, January);

        // Assert
        Assert.True(version.AppliesOn(DateOnly.FromDateTime(January)));
        Assert.Same(version, framework.CurrentVersion(DateOnly.FromDateTime(January)));
    }

    [Fact]
    public void A_framework_whose_only_version_has_not_started_uses_that_version()
    {
        // Arrange
        var framework = Framework.Create(Guid.NewGuid(), "CRA", PdfFile(1), AnnaId, January);
        var version = framework.LatestVersion;
        version.AddRequirement("I.1", "Secure by design", null);
        version.SetAppliesFrom(new DateOnly(2027, 12, 11));

        // Act
        version.Activate(AnnaId, January);

        // Assert
        Assert.Same(version, framework.CurrentVersion(new DateOnly(2027, 1, 15)));
        Assert.Null(framework.UpcomingVersion(new DateOnly(2027, 1, 15)));
    }

    [Fact]
    public void The_date_can_be_changed_while_the_version_is_a_draft()
    {
        // Arrange
        var framework = Framework.Create(Guid.NewGuid(), "Security policy", PdfFile(1), AnnaId, January);
        var draft = framework.LatestVersion;

        // Act
        draft.SetAppliesFrom(StartDate);
        draft.SetAppliesFrom(StartDate.AddMonths(1));

        // Assert
        Assert.Equal(new DateOnly(2027, 3, 1), draft.AppliesFrom);
    }

    [Fact]
    public void The_date_is_locked_when_the_version_is_activated()
    {
        // Arrange
        var (_, _, second) = PolicyWithUpcomingVersion();

        // Act
        var error = Assert.Throws<DomainException>(() => second.SetAppliesFrom(StartDate.AddMonths(1)));

        // Assert
        Assert.Equal("An active version cannot be changed", error.Message);
        Assert.Equal(StartDate, second.AppliesFrom);
    }
}
