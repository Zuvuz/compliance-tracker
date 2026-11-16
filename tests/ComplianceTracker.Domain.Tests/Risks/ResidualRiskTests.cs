using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Domain.Tests.Risks;

// US-165: edge cases of the residual risk. Arrange / Act / Assert.
public class ResidualRiskTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Risk NewRisk(int likelihood = 4, int impact = 5, RiskTreatment treatment = RiskTreatment.Reduce)
        => Risk.Create(Guid.NewGuid(), "Unauthorized remote access", null, likelihood, impact, Anna, treatment, Anna, Monday);

    [Fact]
    public void Without_a_residual_it_shows_a_dash()
    {
        var risk = NewRisk();

        Assert.Null(risk.ResidualLevel);
        Assert.Equal("–", risk.ResidualText);
    }

    [Fact]
    public void Setting_the_residual_keeps_the_level_before_the_measures()
    {
        var risk = NewRisk();

        var change = risk.SetResidualRisk(2, 5);

        Assert.Equal(RiskLevel.Critical, risk.Level);
        Assert.Equal(RiskLevel.High, risk.ResidualLevel);
        Assert.Equal("Residual risk: – → 2 × 5 (High)", change!.ToString());
    }

    [Fact]
    public void A_residual_higher_than_before_the_measures_is_refused()
        => Assert.Equal(Risk.ResidualTooHigh, Assert.Throws<DomainException>(() => NewRisk().SetResidualRisk(5, 5)).Message);

    [Fact]
    public void A_residual_equal_to_before_the_measures_is_allowed()
        => Assert.Equal(RiskLevel.Critical, Value(NewRisk(), 4, 5));

    [Fact]
    public void A_higher_residual_through_the_edit_form_is_refused_too()
        => Assert.Throws<DomainException>(() => NewRisk(2, 2).Update("T", null, 2, 2, Anna, RiskTreatment.Reduce, 3, 3));

    [Fact]
    public void The_same_residual_again_is_no_change()
    {
        var risk = NewRisk();
        risk.SetResidualRisk(2, 5);

        Assert.Null(risk.SetResidualRisk(2, 5));
    }

    [Fact]
    public void A_backed_risk_to_reduce_without_a_residual_asks_for_one()
    {
        var risk = NewRisk();
        risk.AddMeasure(Guid.NewGuid(), null);

        Assert.True(risk.ResidualToSet(measuresBacked: true));
        Assert.False(risk.ResidualToSet(measuresBacked: false));
    }

    [Fact]
    public void A_risk_without_measures_or_with_a_residual_does_not_ask()
    {
        var risk = NewRisk();
        Assert.False(risk.ResidualToSet(measuresBacked: true));

        risk.AddMeasure(Guid.NewGuid(), null);
        risk.SetResidualRisk(2, 2);
        Assert.False(risk.ResidualToSet(measuresBacked: true));
    }

    [Fact]
    public void An_accepted_risk_does_not_ask()
    {
        var risk = NewRisk(treatment: RiskTreatment.Accept);
        risk.AddMeasure(Guid.NewGuid(), null);

        Assert.False(risk.ResidualToSet(measuresBacked: true));
    }

    [Fact]
    public void Removing_a_measure_after_the_residual_was_set_asks_to_review_it()
    {
        var risk = NewRisk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);
        risk.SetResidualRisk(2, 5);

        risk.RemoveMeasure(measure.Id);

        Assert.True(risk.ResidualToReview);
    }

    [Fact]
    public void Removing_a_measure_without_a_residual_does_not_ask_to_review()
    {
        var risk = NewRisk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);

        risk.RemoveMeasure(measure.Id);

        Assert.False(risk.ResidualToReview);
    }

    [Fact]
    public void Setting_the_residual_again_ends_the_review_even_with_the_same_values()
    {
        var risk = NewRisk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);
        risk.SetResidualRisk(2, 5);
        risk.RemoveMeasure(measure.Id);

        var change = risk.SetResidualRisk(2, 5);

        Assert.NotNull(change);
        Assert.False(risk.ResidualToReview);
    }

    [Fact]
    public void A_closed_risk_cannot_get_a_residual()
    {
        var risk = NewRisk();
        risk.Close("Gone", Monday);

        Assert.Throws<DomainException>(() => risk.SetResidualRisk(1, 1));
    }

    private static RiskLevel? Value(Risk risk, int likelihood, int impact)
    {
        risk.SetResidualRisk(likelihood, impact);
        return risk.ResidualLevel;
    }
}
