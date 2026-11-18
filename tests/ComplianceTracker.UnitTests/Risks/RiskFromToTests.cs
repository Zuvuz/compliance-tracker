using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// US-203: what a risk has gone from and to, and its history from the activity log. Edge cases. Arrange / Act / Assert.
public class RiskFromToTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();

    // ---------------------------------------------------------- R1, R3: the score from and to

    [Fact]
    public void Without_a_residual_the_score_is_the_risk_itself_and_it_stays_in_its_place()
    {
        var risk = Risk();

        Assert.False(risk.HasResidual);
        Assert.Equal("3 × 4 = 12", risk.ScoreText);
        Assert.Equal((3, 4), (risk.LikelihoodAfter, risk.ImpactAfter));
    }

    [Fact]
    public void With_a_residual_the_score_goes_from_and_to_and_the_risk_moves()
    {
        var risk = Risk();
        risk.SetResidualRisk(2, 3);

        Assert.True(risk.HasResidual);
        Assert.Equal("3 × 4 = 12 → 2 × 3 = 6", risk.ScoreText);
        Assert.Equal((2, 3), (risk.LikelihoodAfter, risk.ImpactAfter));
    }

    [Fact]
    public void A_residual_equal_to_the_risk_still_shows_both()
    {
        var risk = Risk();
        risk.SetResidualRisk(3, 4);

        Assert.Equal("3 × 4 = 12 → 3 × 4 = 12", risk.ScoreText);
    }

    // ---------------------------------------------------------- R4, R5: the history

    [Fact]
    public void The_history_has_the_risks_own_entries_newest_first()
    {
        var entries = new[]
        {
            Entry(ActivityAction.RiskAdded, "Session Hijacking: likelihood 3, impact 4 (High), AI-drafted", 0),
            Entry(ActivityAction.RiskResidualSet, "Session Hijacking: Residual risk: – → 2 × 3 (Medium)", 2),
            Entry(ActivityAction.RiskReviewed, "Session Hijacking", 1)
        };

        var history = RiskHistory.For("Session Hijacking", entries);

        Assert.Equal(["Residual risk", "Reviewed", "Added"], history.Select(h => h.Kind));
        Assert.Equal("Residual risk: – → 2 × 3 (Medium)", history[0].What);
        Assert.Equal("", history[1].What);
        Assert.Equal(Anna, history[0].UserId);
    }

    [Fact]
    public void Another_risk_whose_title_starts_the_same_is_left_out()
    {
        var entries = new[]
        {
            Entry(ActivityAction.RiskReviewed, "Session", 0),
            Entry(ActivityAction.RiskUpdated, "Session: Owner: Anna → Bo", 1),
            Entry(ActivityAction.RiskUpdated, "Session Hijacking stolen: Owner: Anna → Bo", 2)
        };

        Assert.Empty(RiskHistory.For("Session Hijacking", entries));
    }

    [Fact]
    public void Entries_written_under_an_earlier_title_count_after_a_rename()
    {
        var entries = new[]
        {
            Entry(ActivityAction.RiskAdded, "Stolen cookie: likelihood 3, impact 4 (High)", 0),
            Entry(ActivityAction.RiskUpdated, "Session Hijacking: Title: Stolen cookie → Session Hijacking; Impact: 4 → 5", 1),
            Entry(ActivityAction.RiskReviewed, "Session Hijacking", 2)
        };

        var history = RiskHistory.For("Session Hijacking", entries);

        Assert.Equal(["Reviewed", "Changed", "Added"], history.Select(h => h.Kind));
    }

    [Fact]
    public void Entries_that_are_not_about_one_risk_are_left_out()
    {
        var entries = new[]
        {
            Entry(ActivityAction.RisksImported, "Session Hijacking: 3 risks", 0),
            Entry(ActivityAction.RiskDraftDiscarded, "Session Hijacking: not ours", 1),
            new ActivityLogEntry(Organisation, Anna, ActivityAction.RiskReviewed, Monday, Project)
        };

        Assert.Empty(RiskHistory.For("Session Hijacking", entries));
    }

    [Fact]
    public void A_withdrawn_risk_keeps_its_history_and_the_system_has_no_user()
    {
        var entries = new[]
        {
            Entry(ActivityAction.RiskAdded, "Session Hijacking: likelihood 3, impact 4 (High)", 0),
            new ActivityLogEntry(Organisation, null, ActivityAction.RiskWithdrawn, Monday.AddDays(1), Project, "Session Hijacking: Added twice")
        };

        var history = RiskHistory.For("Session Hijacking", entries);

        Assert.Equal(("Withdrawn", "Added twice", (Guid?)null), (history[0].Kind, history[0].What, history[0].UserId));
    }

    private static Risk Risk() => Domain.Risks.Risk.Create(Project, "Session Hijacking", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);

    private static ActivityLogEntry Entry(ActivityAction action, string details, int day)
        => new(Organisation, Anna, action, Monday.AddDays(day), Project, details);
}
