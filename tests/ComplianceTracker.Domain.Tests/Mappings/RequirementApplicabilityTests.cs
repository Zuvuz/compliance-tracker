using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of RequirementApplicability (US-043 R5).
// Every test follows Arrange / Act / Assert.
public class RequirementApplicabilityTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Not_applicable_keeps_the_reason_who_and_when()
    {
        // Arrange
        var anna = Guid.NewGuid();

        // Act
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "  No network interface ", anna, Monday);

        // Assert
        Assert.True(decision.IsActive);
        Assert.Equal("No network interface", decision.Reason);
        Assert.Equal(anna, decision.DecidedById);
        Assert.Equal(Monday, decision.DecidedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Not_applicable_needs_a_reason(string reason)
    {
        // Arrange (nothing)

        // Act
        var error = Assert.Throws<DomainException>(
            () => RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), reason, Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("Give a reason why the requirement does not apply", error.Message);
    }

    [Fact]
    public void Making_it_applicable_again_keeps_the_decision()
    {
        // Arrange
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "Reason", Guid.NewGuid(), Monday);
        var bo = Guid.NewGuid();

        // Act
        decision.Revoke(bo, Monday.AddDays(1));

        // Assert
        Assert.False(decision.IsActive);
        Assert.Equal(bo, decision.RevokedById);
        Assert.Equal("Reason", decision.Reason);
        Assert.Throws<DomainException>(() => decision.Revoke(bo, Monday.AddDays(2)));
    }

    // ---------------------------------------------------------- US-144 R14: a risk as the justification

    [Fact]
    public void A_risk_can_justify_not_applicable_and_is_carried_to_the_next_version()
    {
        // Arrange
        var risk = Guid.NewGuid();
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "No remote access", Guid.NewGuid(), Monday, riskId: risk);

        // Act
        var carried = decision.CarryTo(Guid.NewGuid(), textChanged: true);

        // Assert
        Assert.Equal(risk, decision.RiskId);
        Assert.Equal(risk, carried.RiskId);
        Assert.True(carried.NeedsReview);
    }

    [Fact]
    public void Without_a_risk_the_reason_alone_is_enough()
    {
        // Act
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "No remote access", Guid.NewGuid(), Monday);

        // Assert
        Assert.Null(decision.RiskId);
    }
}
