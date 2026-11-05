using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Domain.Tests.Risks;

// Unit tests of Risk (US-042). Every test follows Arrange / Act / Assert.
public class RiskTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Risk Create(int likelihood = 3, int impact = 4, RiskTreatment treatment = RiskTreatment.Reduce)
        => Risk.Create(Guid.NewGuid(), "Stolen session cookie", null, likelihood, impact, Anna, treatment, Anna, Monday);

    [Theory]
    [InlineData(1, 4, RiskLevel.Low)]
    [InlineData(1, 5, RiskLevel.Medium)]
    [InlineData(3, 3, RiskLevel.Medium)]
    [InlineData(2, 5, RiskLevel.High)]
    [InlineData(4, 4, RiskLevel.High)]
    [InlineData(4, 5, RiskLevel.Critical)]
    public void The_level_follows_likelihood_times_impact(int likelihood, int impact, RiskLevel level)
    {
        // Arrange (nothing)

        // Act
        var risk = Create(likelihood, impact);

        // Assert
        Assert.Equal(level, risk.Level);
    }

    [Theory]
    [InlineData("", 3, 3, "Risk title is required")]
    [InlineData("Risk", 0, 3, "Likelihood must be from 1 to 5")]
    [InlineData("Risk", 3, 6, "Impact must be from 1 to 5")]
    public void A_risk_needs_a_title_and_values_on_the_scale(string title, int likelihood, int impact, string message)
    {
        // Arrange (nothing)

        // Act
        var error = Assert.Throws<DomainException>(
            () => Risk.Create(Guid.NewGuid(), title, null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, Monday));

        // Assert
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Update_returns_the_old_and_new_values()
    {
        // Arrange
        var risk = Create();

        // Act
        var changes = risk.Update("Stolen session cookie", null, 2, 4, Anna, RiskTreatment.Reduce, null, null);

        // Assert
        Assert.Equal("Likelihood: 3 → 2", Assert.Single(changes).ToString());
    }

    [Fact]
    public void The_residual_risk_needs_both_values()
    {
        // Arrange
        var risk = Create();

        // Act
        var error = Assert.Throws<DomainException>(() => risk.Update("Stolen session cookie", null, 3, 4, Anna, RiskTreatment.Reduce, 2, null));

        // Assert
        Assert.Equal("Give both the residual likelihood and impact, or neither", error.Message);
    }

    [Fact]
    public void Accepting_needs_a_reason_and_keeps_who()
    {
        // Arrange
        var risk = Create();

        // Act
        var noReason = Assert.Throws<DomainException>(() => risk.Accept(" ", Anna, Monday));
        risk.Accept("Retired in Q1", Anna, Monday);

        // Assert
        Assert.Equal("Give a reason for accepting the risk", noReason.Message);
        Assert.Equal(RiskStatus.Accepted, risk.Status);
        Assert.Equal(RiskTreatment.Accept, risk.Treatment);
        Assert.Equal(Anna, risk.AcceptedById);
    }

    [Fact]
    public void A_risk_to_reduce_is_treated_once_it_has_a_measure()
    {
        // Arrange
        var risk = Create();
        var before = (risk.HasNoMeasures, risk.DisplayStatus);

        // Act
        risk.AddMeasure(null, Guid.NewGuid());

        // Assert
        Assert.Equal((true, "Open"), before);
        Assert.False(risk.HasNoMeasures);
        Assert.Equal("Treated", risk.DisplayStatus);
    }

    [Fact]
    public void A_measure_is_one_requirement_or_one_story()
    {
        // Arrange
        var risk = Create();

        // Act
        var error = Assert.Throws<DomainException>(() => risk.AddMeasure(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        Assert.Equal("Choose a requirement or a user story", error.Message);
    }

    [Fact]
    public void A_review_is_due_after_twelve_months()
    {
        // Arrange
        var risk = Create();
        risk.MarkReviewed(Anna, Monday);

        // Act
        var dueAfterAYear = risk.IsReviewDue(Monday.AddMonths(12).AddDays(1));
        var dueBefore = risk.IsReviewDue(Monday.AddMonths(11));

        // Assert
        Assert.True(dueAfterAYear);
        Assert.False(dueBefore);
    }

    [Fact]
    public void A_closed_risk_is_kept_and_cannot_be_changed()
    {
        // Arrange
        var risk = Create();

        // Act
        risk.Close("API removed", Monday);
        var error = Assert.Throws<DomainException>(() => risk.MarkReviewed(Anna, Monday));

        // Assert
        Assert.Equal(RiskStatus.Closed, risk.Status);
        Assert.Equal("A closed risk cannot be changed", error.Message);
        Assert.False(risk.IsReviewDue(Monday.AddYears(5)));
    }
}
