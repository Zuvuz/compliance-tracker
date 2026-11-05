using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of end of support and retention (US-074, CRA Art. 13(13)). Every test follows Arrange / Act / Assert.
public class RetentionTests
{
    private static readonly DateOnly Released = new(2026, 11, 1);

    private static Project ReleasedProject(DateOnly supportEnds)
    {
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", null, "A customer portal", null, LifecyclePhase.Development);
        project.SetSupportPeriod(supportEnds, null, Released);
        project.ChangePhase(LifecyclePhase.Released, Released);
        return project;
    }

    [Fact]
    public void The_project_is_kept_ten_years_after_it_was_last_placed_on_the_market()
    {
        // Arrange
        var project = ReleasedProject(new DateOnly(2031, 12, 31));

        // Act
        project.EndSupport(new DateOnly(2026, 12, 31), null, new DateOnly(2032, 1, 10));

        // Assert
        Assert.Equal(new DateOnly(2036, 12, 31), project.RetainUntil);
        Assert.Equal(LifecyclePhase.EndOfSupport, project.Phase);
        Assert.Equal(new DateOnly(2032, 1, 10), project.EndOfSupportOn);
    }

    [Fact]
    public void A_longer_support_period_keeps_the_project_longer()
    {
        // Arrange
        var project = ReleasedProject(new DateOnly(2040, 6, 30));

        // Act
        project.EndSupport(new DateOnly(2027, 1, 1), null, new DateOnly(2027, 1, 10));

        // Assert
        Assert.Equal(new DateOnly(2040, 6, 30), project.RetainUntil);
    }

    [Fact]
    public void An_earlier_retention_date_is_refused()
    {
        // Arrange
        var project = ReleasedProject(new DateOnly(2031, 12, 31));

        // Act / Assert
        var error = Assert.Throws<DomainException>(() =>
            project.EndSupport(new DateOnly(2026, 12, 31), new DateOnly(2035, 1, 1), new DateOnly(2032, 1, 10)));
        Assert.Equal("The project must be kept until at least 31-12-2036", error.Message);
        Assert.Equal(LifecyclePhase.Released, project.Phase);
    }

    [Fact]
    public void The_last_day_on_the_market_cannot_be_in_the_future_or_before_release()
    {
        // Arrange
        var project = ReleasedProject(new DateOnly(2031, 12, 31));

        // Act / Assert
        Assert.Throws<DomainException>(() => project.EndSupport(new DateOnly(2033, 1, 1), null, new DateOnly(2032, 1, 10)));
        Assert.Throws<DomainException>(() => project.EndSupport(new DateOnly(2026, 1, 1), null, new DateOnly(2032, 1, 10)));
    }

    [Fact]
    public void The_project_cannot_be_removed_before_the_retention_date()
    {
        // Arrange
        var project = ReleasedProject(new DateOnly(2031, 12, 31));
        project.EndSupport(new DateOnly(2026, 12, 31), null, new DateOnly(2032, 1, 10));

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => project.EnsureMayBeRemoved(new DateOnly(2036, 12, 31)));
        Assert.Equal("The project must be kept until 31-12-2036", error.Message);
        project.EnsureMayBeRemoved(new DateOnly(2037, 1, 1));
    }
}
