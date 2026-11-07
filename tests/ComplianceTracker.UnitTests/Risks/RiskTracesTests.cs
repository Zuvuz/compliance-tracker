using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// Edge cases of which risks reach a requirement (US-124 R2, R3). Arrange / Act / Assert.
public class RiskTracesTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Risk Risk(string title, int likelihood = 3, int impact = 4)
        => Domain.Risks.Risk.Create(Guid.NewGuid(), title, null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, Monday);

    private static ILookup<Guid, Guid> Covers(params (Guid Story, Guid Requirement)[] links)
        => links.ToLookup(l => l.Story, l => l.Requirement);

    [Fact]
    public void A_requirement_that_is_a_measure_is_reached_directly()
    {
        // Arrange
        var requirement = Guid.NewGuid();
        var risk = Risk("Phishing");
        risk.AddMeasure(requirement, null);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers());

        // Assert
        Assert.Equal("Phishing", Assert.Single(reached[requirement]).Title);
    }

    [Fact]
    public void A_requirement_is_reached_through_the_story_of_an_action()
    {
        // Arrange
        var (story, requirement) = (Guid.NewGuid(), Guid.NewGuid());
        var risk = Risk("Stolen sessions");
        var action = risk.AddAction("Short sessions", Anna, null, Anna, Monday);
        risk.LinkAction(action.Id, story, null);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers((story, requirement)));

        // Assert
        Assert.Single(reached[requirement]);
    }

    [Fact]
    public void A_requirement_is_reached_through_a_story_that_is_a_measure()
    {
        // Arrange
        var (story, requirement) = (Guid.NewGuid(), Guid.NewGuid());
        var risk = Risk("Stolen sessions");
        risk.AddMeasure(null, story);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers((story, requirement)));

        // Assert
        Assert.Single(reached[requirement]);
    }

    [Fact]
    public void A_risk_that_reaches_a_requirement_in_several_ways_is_named_once()
    {
        // Arrange
        var (story, requirement) = (Guid.NewGuid(), Guid.NewGuid());
        var risk = Risk("Stolen sessions");
        risk.AddMeasure(requirement, null);
        risk.AddMeasure(null, story);
        var one = risk.AddAction("Short sessions", Anna, null, Anna, Monday);
        var two = risk.AddAction("MFA", Anna, null, Anna, Monday);
        risk.LinkAction(one.Id, story, null);
        risk.LinkAction(two.Id, story, null);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers((story, requirement)));

        // Assert
        Assert.Single(reached[requirement]);
    }

    [Fact]
    public void Closed_risks_reach_nothing()
    {
        // Arrange
        var requirement = Guid.NewGuid();
        var risk = Risk("Old risk");
        risk.AddMeasure(requirement, null);
        risk.Close("No longer relevant", Monday);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers());

        // Assert
        Assert.False(reached.ContainsKey(requirement));
    }

    [Fact]
    public void An_action_with_only_evidence_or_a_story_that_covers_nothing_reaches_nothing()
    {
        // Arrange
        var risk = Risk("Stolen sessions");
        var action = risk.AddAction("Short sessions", Anna, null, Anna, Monday);
        risk.LinkAction(action.Id, null, Guid.NewGuid());
        risk.LinkAction(action.Id, Guid.NewGuid(), null);

        // Act
        var reached = RiskTraces.ByRequirement([risk], Covers());

        // Assert
        Assert.Empty(reached);
    }

    [Fact]
    public void The_highest_risk_comes_first_and_equal_ones_by_title()
    {
        // Arrange
        var requirement = Guid.NewGuid();
        var low = Risk("Low one", 1, 2);
        var highB = Risk("B high", 4, 4);
        var highA = Risk("A high", 4, 4);
        foreach (var risk in new[] { low, highB, highA })
        {
            risk.AddMeasure(requirement, null);
        }

        // Act
        var reached = RiskTraces.ByRequirement([low, highB, highA], Covers());

        // Assert
        Assert.Equal(["A high", "B high", "Low one"], reached[requirement].Select(r => r.Title));
    }

    [Fact]
    public void No_risks_give_an_empty_result_and_an_empty_text()
    {
        // Arrange (nothing)

        // Act
        var reached = RiskTraces.ByRequirement([], Covers());

        // Assert
        Assert.Empty(reached);
        Assert.Equal("", RiskTraces.Text([]));
    }

    [Fact]
    public void The_text_names_title_and_level()
    {
        // Arrange
        RiskReference[] risks = [new(Guid.NewGuid(), "Stolen sessions", RiskLevel.High), new(Guid.NewGuid(), "Phishing", RiskLevel.Medium)];

        // Act
        var text = RiskTraces.Text(risks);

        // Assert
        Assert.Equal("Stolen sessions (High); Phishing (Medium)", text);
    }
}
