using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// Unit tests of the Risks tab's register after the filters (US-229, moved from ProjectsController.Details unchanged:
// US-042 R7, R8, US-147, US-159 R4, US-203 R2). Every test follows Arrange / Act / Assert.
public class RiskTabTests
{
    private static readonly DateTime Created = new(2025, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();

    private static Risk Risk(string title, int likelihood = 3, int impact = 3, DateTime? created = null)
        => Domain.Risks.Risk.Create(Project, title, null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, created ?? Now);

    private static IReadOnlyDictionary<Guid, RiskBacking> NoBacking => new Dictionary<Guid, RiskBacking>();

    private static List<string> Titles(IEnumerable<Risk> risks) => risks.Select(r => r.Title).ToList();

    [Fact]
    public void Without_filters_every_risk_but_the_closed_ones_is_shown()
    {
        var closed = Risk("Old TLS");
        closed.Close("Retired", Now);

        var shown = RiskRegister.Filter([Risk("Phishing"), closed], NoBacking, new RiskTabRequest(), Now);

        Assert.Equal(["Phishing"], Titles(shown));
    }

    [Fact]
    public void An_empty_register_shows_nothing()
        => Assert.Empty(RiskRegister.Filter([], NoBacking, new RiskTabRequest(Status: "Closed", OnlyReviewDue: true), Now));

    [Theory]
    [InlineData("Closed", "Old TLS")]
    [InlineData("closed", "Old TLS")]
    [InlineData("Accepted", "Legacy API")]
    [InlineData("Open", "Phishing")]
    public void The_status_filter_matches_the_shown_status_in_any_case(string status, string expected)
    {
        var closed = Risk("Old TLS");
        closed.Close("Retired", Now);
        var accepted = Risk("Legacy API");
        accepted.Accept("Retired in Q1", Anna, Now);

        var shown = RiskRegister.Filter([Risk("Phishing"), closed, accepted], NoBacking, new RiskTabRequest(Status: status), Now);

        Assert.Equal([expected], Titles(shown));
    }

    [Fact]
    public void A_risk_to_reduce_with_measures_is_found_as_Treated_not_Open()
    {
        var treated = Risk("Stolen cookie");
        treated.AddMeasure(Guid.NewGuid(), null);

        Assert.Empty(RiskRegister.Filter([treated], NoBacking, new RiskTabRequest(Status: "Open"), Now));
        Assert.Single(RiskRegister.Filter([treated], NoBacking, new RiskTabRequest(Status: "Treated"), Now));
    }

    // Unchanged from before US-229: an unknown status or level matches no risk
    [Theory]
    [InlineData("Lost", null)]
    [InlineData(null, "Extreme")]
    public void An_unknown_status_or_level_shows_no_risks(string? status, string? level)
        => Assert.Empty(RiskRegister.Filter([Risk("Phishing")], NoBacking, new RiskTabRequest(status, level), Now));

    [Fact]
    public void The_level_filter_shows_only_that_level_in_any_case()
    {
        var shown = RiskRegister.Filter([Risk("Low one", 1, 1), Risk("Critical one", 5, 5)], NoBacking, new RiskTabRequest(Level: "critical"), Now);

        Assert.Equal(["Critical one"], Titles(shown));
    }

    [Fact]
    public void Review_due_shows_only_risks_not_reviewed_for_a_year()
    {
        var shown = RiskRegister.Filter([Risk("Old", created: Created), Risk("New")], NoBacking, new RiskTabRequest(OnlyReviewDue: true), Now);

        Assert.Equal(["Old"], Titles(shown));
    }

    [Fact]
    public void Nothing_behind_it_shows_only_the_risks_marked_so_and_none_without_backing()
    {
        var bare = Risk("Bare");
        var backed = Risk("Backed");
        var unknown = Risk("Unknown");
        var backing = new Dictionary<Guid, RiskBacking>
        {
            [bare.Id] = new(0, 0, 0, true),
            [backed.Id] = new(1, 1, 1, false)
        };

        var shown = RiskRegister.Filter([bare, backed, unknown], backing, new RiskTabRequest(OnlyNothingBehind: true), Now);

        Assert.Equal(["Bare"], Titles(shown));
    }

    [Fact]
    public void A_cell_filters_by_likelihood_and_impact_before_the_measures()
    {
        var shown = RiskRegister.Filter([Risk("Here", 3, 4), Risk("There", 4, 3)], NoBacking, new RiskTabRequest(Cell: (3, 4)), Now);

        Assert.Equal(["Here"], Titles(shown));
    }

    [Fact]
    public void After_the_measures_a_cell_filters_by_the_residual_and_a_risk_without_one_by_its_own_place()
    {
        var reduced = Risk("Reduced", 4, 4);
        reduced.Update("Reduced", null, 4, 4, Anna, RiskTreatment.Reduce, 2, 2);
        var withoutResidual = Risk("Without residual", 2, 2);

        var after = RiskRegister.Filter([reduced, withoutResidual], NoBacking, new RiskTabRequest(Cell: (2, 2), MatrixAfter: true), Now);
        var before = RiskRegister.Filter([reduced, withoutResidual], NoBacking, new RiskTabRequest(Cell: (2, 2)), Now);

        Assert.Equal(["Reduced", "Without residual"], Titles(after));
        Assert.Equal(["Without residual"], Titles(before));
    }

    [Fact]
    public void The_register_is_sorted_by_score_then_by_title_ignoring_case()
    {
        var shown = RiskRegister.Filter([Risk("b low", 1, 2), Risk("Zulu", 5, 5), Risk("alpha", 5, 5), Risk("A low", 1, 2)], NoBacking, new RiskTabRequest(), Now);

        Assert.Equal(["alpha", "Zulu", "A low", "b low"], Titles(shown));
    }

    [Fact]
    public void Several_filters_together_must_all_match()
    {
        var match = Risk("Match", 5, 5, Created);
        var wrongLevel = Risk("Wrong level", 1, 1, Created);
        var notDue = Risk("Not due", 5, 5);

        var shown = RiskRegister.Filter([match, wrongLevel, notDue], NoBacking, new RiskTabRequest(Level: "Critical", OnlyReviewDue: true, Cell: (5, 5)), Now);

        Assert.Equal(["Match"], Titles(shown));
    }
}
