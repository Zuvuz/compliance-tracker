using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of the support period (US-025, CRA Art. 13(8)). Every test follows Arrange / Act / Assert.
public class SupportPeriodTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static Project NewProject()
        => Project.Create(Guid.NewGuid(), "Acme Portal", null, "A customer portal", null, LifecyclePhase.Development);

    [Fact]
    public void Release_needs_a_support_end_date()
    {
        // Arrange
        var project = NewProject();

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => project.ChangePhase(LifecyclePhase.Released, Today));
        Assert.Equal("Set the support end date before release", error.Message);
        Assert.Equal(LifecyclePhase.Development, project.Phase);
    }

    [Fact]
    public void Release_records_the_release_day()
    {
        // Arrange
        var project = NewProject();
        project.SetSupportPeriod(Today.AddYears(5), null, Today);

        // Act
        project.ChangePhase(LifecyclePhase.Released, Today);

        // Assert
        Assert.Equal(Today, project.ReleasedOn);
        Assert.Equal(Today.AddYears(5), project.SupportEndsOn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Under_five_years_needs_a_reason(string? reason)
    {
        // Arrange
        var project = NewProject();

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => project.SetSupportPeriod(Today.AddYears(3), reason, Today));
        Assert.Equal("A support period under five years needs a reason", error.Message);
    }

    [Fact]
    public void Under_five_years_with_a_reason_is_kept_with_the_reason()
    {
        // Arrange
        var project = NewProject();

        // Act
        project.SetSupportPeriod(Today.AddYears(3), " A campaign site ", Today);

        // Assert
        Assert.Equal("A campaign site", project.SupportPeriodReason);
    }

    [Fact]
    public void The_end_date_cannot_move_earlier()
    {
        // Arrange
        var project = NewProject();
        project.SetSupportPeriod(Today.AddYears(6), null, Today);

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => project.SetSupportPeriod(Today.AddYears(5).AddDays(1), null, Today));
        Assert.Equal("The support end date cannot be moved earlier", error.Message);
    }

    [Fact]
    public void Moving_the_end_date_later_returns_old_and_new()
    {
        // Arrange
        var project = NewProject();
        project.SetSupportPeriod(new DateOnly(2031, 11, 1), null, Today);

        // Act
        var change = project.SetSupportPeriod(new DateOnly(2032, 11, 1), null, Today);

        // Assert
        Assert.Equal("Support until: 01-11-2031 → 01-11-2032", change!.ToString());
    }

    [Fact]
    public void The_end_date_must_be_in_the_future()
    {
        // Arrange
        var project = NewProject();

        // Act / Assert
        Assert.Throws<DomainException>(() => project.SetSupportPeriod(Today, "Anything", Today));
    }

    [Fact]
    public void A_project_released_before_keeps_its_release_day_when_released_again()
    {
        // Arrange
        var project = NewProject();
        project.SetSupportPeriod(Today.AddYears(5), null, Today);
        project.ChangePhase(LifecyclePhase.Released, Today);
        project.ChangePhase(LifecyclePhase.Development, Today.AddDays(10));

        // Act
        project.ChangePhase(LifecyclePhase.Released, Today.AddDays(20));

        // Assert
        Assert.Equal(Today, project.ReleasedOn);
    }
}
