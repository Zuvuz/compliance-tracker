using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// US-157: edge cases of the risk agent's rounds per theme. Arrange / Act / Assert.
public class RiskThemeTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static RiskDraftContext Context(string[]? uncovered = null, string[]? events = null, string[]? findings = null)
        => new("A customer portal", null, [], uncovered ?? [], events ?? [], findings ?? [], []);

    private static DraftedRisk Drafted(string title) => new(title, "threat", "asset", 3, 3, "based on", "why");

    // ---------------------------------------------------------- Which themes are asked (R2)

    [Fact]
    public void The_four_general_themes_are_always_asked()
        => Assert.Equal([RiskThemes.Access, RiskThemes.Data, RiskThemes.Updates, RiskThemes.Availability], RiskThemes.For(Context()));

    [Fact]
    public void Uncovered_requirements_add_their_theme()
        => Assert.Contains(RiskThemes.Requirements, RiskThemes.For(Context(uncovered: ["CRA I.2(d) Access"])));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void An_open_event_or_a_severe_finding_adds_its_theme_once(bool withEvent, bool withFinding)
    {
        var themes = RiskThemes.For(Context(events: withEvent ? ["Phishing"] : null, findings: withFinding ? ["CVE-1 (High)"] : null));

        Assert.Single(themes, RiskThemes.EventsAndFindings);
    }

    // ---------------------------------------------------------- The prompt of a round

    [Fact]
    public void A_round_names_its_theme_and_asks_for_at_most_three()
    {
        var prompt = RiskDraftPrompt.For(Context(), RiskThemes.Data);

        Assert.Contains("Theme: Data and privacy.", prompt);
        Assert.Contains($"with at most {RiskThemes.PerTheme} risks", prompt);
        Assert.Contains("Intended purpose: A customer portal", prompt);
    }

    [Fact]
    public void Without_a_theme_the_prompt_is_the_old_one()
    {
        var prompt = RiskDraftPrompt.For(Context());

        Assert.DoesNotContain("Theme:", prompt);
        Assert.Contains($"with at most {RiskDraftPrompt.MaxDrafts} risks", prompt);
    }

    // ---------------------------------------------------------- Putting the rounds together (R1, R3)

    [Fact]
    public void Each_kept_draft_keeps_the_theme_of_its_round()
    {
        var rounds = new List<(RiskTheme, IReadOnlyList<DraftedRisk>)>
        {
            (RiskThemes.Access, [Drafted("Account takeover")]),
            (RiskThemes.Data, [Drafted("Data leak")])
        };

        var (kept, leftOut) = RiskDraftService.KeepRounds(rounds, [], []);

        Assert.Equal([("Account takeover", RiskThemes.Access.Name), ("Data leak", RiskThemes.Data.Name)], kept.Select(k => (k.Risk.Title, k.Theme.Name)));
        Assert.Empty(leftOut);
    }

    [Fact]
    public void The_same_risk_from_a_later_round_is_left_out_and_the_first_theme_wins()
    {
        var rounds = new List<(RiskTheme, IReadOnlyList<DraftedRisk>)>
        {
            (RiskThemes.Access, [Drafted("Account takeover")]),
            (RiskThemes.Requirements, [Drafted("Account takeover by phishing")])
        };

        var (kept, leftOut) = RiskDraftService.KeepRounds(rounds, [], []);

        Assert.Equal(RiskThemes.Access, Assert.Single(kept).Theme);
        Assert.Equal("Account takeover by phishing", Assert.Single(leftOut).Draft);
    }

    [Fact]
    public void Two_identical_drafts_in_different_rounds_are_kept_once()
    {
        var rounds = new List<(RiskTheme, IReadOnlyList<DraftedRisk>)>
        {
            (RiskThemes.Access, [Drafted("Account takeover")]),
            (RiskThemes.Data, [Drafted("Account takeover")])
        };

        Assert.Single(RiskDraftService.KeepRounds(rounds, [], []).Kept);
    }

    [Fact]
    public void At_most_fifteen_drafts_are_kept_in_one_run()
    {
        var rounds = Enumerable.Range(1, 6)
            .Select(t => (new RiskTheme($"Theme {t}", "focus"), (IReadOnlyList<DraftedRisk>)Enumerable.Range(1, 3).Select(i => Drafted($"Unique{t}x{i} risk{t * 10 + i}")).ToList()))
            .ToList();

        Assert.Equal(RiskThemes.PerRun, RiskDraftService.KeepRounds(rounds, [], []).Kept.Count);
    }

    [Fact]
    public void No_rounds_keep_nothing()
        => Assert.Empty(RiskDraftService.KeepRounds([], [], []).Kept);

    // ---------------------------------------------------------- The draft's theme

    [Theory]
    [InlineData("  Availability ", "Availability")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void A_draft_keeps_its_theme_trimmed_and_none_when_blank(string? theme, string? expected)
        => Assert.Equal(expected, RiskDraft.Create(Guid.NewGuid(), "Risk", "", "", 3, 3, "", "", "m", "v", Monday, theme).Theme);

    [Fact]
    public void A_long_theme_is_cut()
        => Assert.Equal(RiskDraft.MaxThemeLength, RiskDraft.Create(Guid.NewGuid(), "Risk", "", "", 3, 3, "", "", "m", "v", Monday, new string('t', 300)).Theme!.Length);
}
