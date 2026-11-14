using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// US-159: edge cases of what stands behind a risk in the register. Arrange / Act / Assert.
public class RiskBackingTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Requirement = Guid.NewGuid();
    private static readonly Guid OtherRequirement = Guid.NewGuid();
    private static readonly Guid Story = Guid.NewGuid();
    private static readonly Guid Evidence = Guid.NewGuid();

    // likelihood 3 × impact 4 = High
    private static Risk HighRisk(RiskTreatment treatment = RiskTreatment.Reduce, int likelihood = 3)
        => Risk.Create(Guid.NewGuid(), "Account takeover", null, likelihood, 4, Anna, treatment, Anna, Monday);

    private static ProjectLinks Links(
        (Guid Requirement, Guid Story)[]? stories = null, (Guid Requirement, Guid Evidence)[]? evidence = null,
        (Guid Story, Guid Evidence)[]? storyEvidence = null, Guid[]? live = null)
        => new(
            (stories ?? []).ToLookup(x => x.Requirement, x => x.Story),
            (evidence ?? []).ToLookup(x => x.Requirement, x => x.Evidence),
            (storyEvidence ?? []).ToLookup(x => x.Story, x => x.Evidence),
            (live ?? [Story]).ToHashSet());

    [Fact]
    public void A_measure_with_a_story_and_evidence_counts_each_once_and_is_backed()
    {
        var risk = HighRisk();
        risk.AddMeasure(Requirement, null);
        risk.AddMeasure(OtherRequirement, null);

        var backing = RiskBackings.For(risk, Links(
            stories: [(Requirement, Story)],
            evidence: [(Requirement, Evidence), (OtherRequirement, Evidence)]));

        Assert.Equal((2, 1, 1, false), (backing.Measures, backing.Stories, backing.Evidence, backing.NothingBehind));
        Assert.Equal("2 measures · 1 story · 1 evidence", backing.Text);
    }

    [Fact]
    public void A_serious_risk_without_measures_has_nothing_behind_it()
        => Assert.True(RiskBackings.For(HighRisk(), Links()).NothingBehind);

    [Fact]
    public void Measures_without_a_story_or_evidence_are_nothing_behind_it()
    {
        var risk = HighRisk();
        risk.AddMeasure(Requirement, null);

        var backing = RiskBackings.For(risk, Links());

        Assert.Equal((1, 0, 0, true), (backing.Measures, backing.Stories, backing.Evidence, backing.NothingBehind));
    }

    [Fact]
    public void A_story_as_the_measure_brings_its_own_evidence()
    {
        var risk = HighRisk();
        risk.AddMeasure(null, Story);

        var backing = RiskBackings.For(risk, Links(storyEvidence: [(Story, Evidence)]));

        Assert.Equal((1, 1, 1, false), (backing.Measures, backing.Stories, backing.Evidence, backing.NothingBehind));
    }

    [Fact]
    public void A_removed_story_does_not_count()
    {
        var risk = HighRisk();
        risk.AddMeasure(Requirement, null);

        var backing = RiskBackings.For(risk, Links(stories: [(Requirement, Story)], live: []));

        Assert.Equal((0, true), (backing.Stories, backing.NothingBehind));
    }

    [Fact]
    public void An_actions_story_and_evidence_count_too()
    {
        var risk = HighRisk();
        risk.AddMeasure(Requirement, null);
        var action = risk.AddAction("Turn on MFA", Anna, null, Anna, Monday);
        risk.LinkAction(action.Id, Story, null);
        risk.LinkAction(action.Id, null, Evidence);

        var backing = RiskBackings.For(risk, Links());

        Assert.Equal((1, 1, false), (backing.Stories, backing.Evidence, backing.NothingBehind));
    }

    [Theory]
    [InlineData(RiskTreatment.Accept)]
    [InlineData(RiskTreatment.Avoid)]
    [InlineData(RiskTreatment.Transfer)]
    public void A_risk_not_to_reduce_is_never_marked(RiskTreatment treatment)
        => Assert.False(RiskBackings.For(HighRisk(treatment), Links()).NothingBehind);

    [Fact]
    public void A_medium_risk_is_not_marked()
        => Assert.False(RiskBackings.For(HighRisk(likelihood: 2), Links()).NothingBehind); // 2 × 4 = Medium

    [Fact]
    public void An_accepted_or_closed_risk_is_not_marked()
    {
        var accepted = HighRisk();
        accepted.Accept("We live with it", Anna, Monday);
        var closed = HighRisk();
        closed.Close("Gone", Monday);

        Assert.False(RiskBackings.For(accepted, Links()).NothingBehind);
        Assert.False(RiskBackings.For(closed, Links()).NothingBehind);
    }

    [Fact]
    public void The_backing_of_every_risk_is_given_by_its_id()
    {
        var first = HighRisk();
        var second = HighRisk();

        var all = RiskBackings.For([first, second], Links());

        Assert.Equal(2, all.Count);
        Assert.True(all.ContainsKey(second.Id));
        Assert.Equal("0 measures · 0 stories · 0 evidence", all[first.Id].Text);
    }
}
