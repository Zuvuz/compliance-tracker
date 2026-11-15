using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-190: edge cases of the NIS2 gap report. Arrange / Act / Assert.
public class Nis2GapReportTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> None = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IReadOnlyList<string>> NoEvidence = new(StringComparer.OrdinalIgnoreCase);

    private static GapAnswer Answer(string code, Maturity maturity)
        => GapAnswer.Create(Guid.NewGuid(), code, maturity, null, Guid.NewGuid(), UserRole.Admin, Monday);

    // ---------------------------------------------------------- Status (R1)

    [Theory]
    [InlineData(true, null, GapStatus.Met)]
    [InlineData(true, Maturity.Implemented, GapStatus.Met)]
    [InlineData(true, Maturity.Defined, GapStatus.PartlyMet)]
    [InlineData(false, Maturity.Measured, GapStatus.PartlyMet)]
    [InlineData(false, Maturity.Defined, GapStatus.PartlyMet)]
    [InlineData(false, Maturity.AdHoc, GapStatus.Missing)]
    [InlineData(false, null, GapStatus.Missing)]
    public void The_status_comes_from_the_evidence_and_the_answer(bool evidence, Maturity? maturity, GapStatus status)
        => Assert.Equal(status, Nis2GapReport.Status(evidence, maturity));

    [Fact]
    public void Everything_met_gives_an_empty_action_plan()
    {
        var all = Nis2Questions.All.Select(q => q.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = Nis2GapReport.Lines(GapQuestionRules.Views(all, []), NoEvidence, None, null);

        var plan = Nis2GapReport.Document("Acme ApS", new DateOnly(2026, 11, 16), lines).Sheets.Single(s => s.Name == "Action plan");

        Assert.Empty(plan.Rows);
    }

    [Fact]
    public void Nothing_met_lists_every_measure_with_high_priority()
    {
        var lines = Nis2GapReport.Lines(GapQuestionRules.Views(None, []), NoEvidence, None, null);

        Assert.All(lines, l => Assert.Equal(GapPriority.High, l.Priority));
        Assert.Equal(Nis2Questions.All.Count, Nis2GapReport.Document("Acme ApS", new DateOnly(2026, 11, 16), lines).Sheets.Single(s => s.Name == "Action plan").Rows.Count);
    }

    [Fact]
    public void A_partly_met_measure_with_a_high_risk_gets_high_priority()
    {
        var lines = Nis2GapReport.Lines(GapQuestionRules.Views(None, [Answer("Art. 21(2)(d)", Maturity.Defined)]), NoEvidence,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Art. 21(2)(d)" }, null);

        Assert.Equal(GapPriority.High, lines.Single(l => l.Code == "Art. 21(2)(d)").Priority);
    }

    [Fact]
    public void Every_measure_has_its_own_action()
        => Assert.All(Nis2Questions.All, q => Assert.NotEqual(Nis2GapReport.ActionFor("unknown"), Nis2GapReport.ActionFor(q.Code)));

    // ---------------------------------------------------------- Change since the last report (R4)

    [Theory]
    [InlineData(GapStatus.PartlyMet, GapStatus.Missing, "Better")]
    [InlineData(GapStatus.Missing, GapStatus.Met, "Worse")]
    [InlineData(GapStatus.Met, GapStatus.Met, "Same")]
    public void The_change_is_compared_with_the_last_report(GapStatus now, GapStatus before, string change)
        => Assert.Equal(change, Nis2GapReport.Change(now, new Dictionary<string, GapStatus> { ["Art. 21(2)(d)"] = before }, "Art. 21(2)(d)"));

    [Fact]
    public void The_first_report_has_no_change_and_a_new_measure_is_new()
    {
        Assert.Null(Nis2GapReport.Change(GapStatus.Met, null, "Art. 21(2)(d)"));
        Assert.Equal("New", Nis2GapReport.Change(GapStatus.Met, new Dictionary<string, GapStatus>(), "Art. 21(2)(d)"));
    }

    [Fact]
    public void Two_reports_with_no_change_say_same_everywhere()
    {
        var lines = Nis2GapReport.Lines(GapQuestionRules.Views(None, []), NoEvidence, None, null);
        var previous = Nis2GapReport.ReadFigures(Nis2GapReport.Figures(lines));

        Assert.All(Nis2GapReport.Lines(GapQuestionRules.Views(None, []), NoEvidence, None, previous), l => Assert.Equal("Same", l.Change));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Unreadable_figures_mean_no_earlier_report(string? figures)
        => Assert.Null(Nis2GapReport.ReadFigures(figures));
}
