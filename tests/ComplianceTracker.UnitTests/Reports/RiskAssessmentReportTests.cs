using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Reports;

// Unit tests of the risk assessment report (US-054). Every test follows Arrange / Act / Assert.
public class RiskAssessmentReportTests
{
    private static readonly DateTime Monday = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Project Portal = Project.Create(Guid.NewGuid(), "Acme Portal", null, "A portal", null, LifecyclePhase.Development);

    private static readonly RiskAssessmentReport.Names Names = new(
        id => id == Anna ? "Anna" : "Unknown",
        _ => "CRA I.2(d)",
        _ => "[US-004] Sign in");

    private static Risk NewRisk(string title, int likelihood, int impact, DateTime? at = null)
        => Risk.Create(Portal.Id, title, null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, at ?? Monday);

    private static ReportDocument Build(params Risk[] risks) => RiskAssessmentReport.Build(Portal, risks, Names, "Anna", Monday);

    private static IReadOnlyList<ReportRow> Sheet(ReportDocument document, string name) => document.Sheets.Single(s => s.Name == name).Rows;

    [Fact]
    public void The_sections_come_in_a_fixed_order_starting_with_the_criteria()
        => Assert.Equal(["Criteria", "Risks", "Accepted risks", "Closed risks"], Build().Sheets.Select(s => s.Name));

    [Fact]
    public void The_criteria_give_the_level_of_every_cell_with_likelihood_5_first()
    {
        var criteria = RiskAssessmentReport.Criteria();

        Assert.Equal(5, criteria.Count);
        Assert.Equal(["5", "Medium (5)", "High (10)", "High (15)", "Critical (20)", "Critical (25)"], criteria[0].Cells);
        Assert.Equal("Low (1)", criteria[4].Cells[1]);
    }

    [Fact]
    public void Risks_are_sorted_by_level_then_score_then_title()
    {
        var document = Build(NewRisk("B phishing", 2, 2), NewRisk("Ransomware", 4, 5), NewRisk("A phishing", 2, 2), NewRisk("Cookie", 3, 4));

        Assert.Equal(["Ransomware", "Cookie", "A phishing", "B phishing"], Sheet(document, "Risks").Select(r => r.Cells[0]));
    }

    [Fact]
    public void A_risk_names_its_measures_actions_owner_and_next_review()
    {
        var risk = NewRisk("Cookie", 3, 4);
        risk.AddMeasure(Guid.NewGuid(), null);
        risk.AddMeasure(null, Guid.NewGuid());

        var row = RiskAssessmentReport.Row(risk, Names, Monday);

        Assert.Equal("High (12)", row.Cells[1]);
        Assert.Equal("Anna", row.Cells[6]);
        Assert.Equal("CRA I.2(d); [US-004] Sign in", row.Cells[7]);
        Assert.Equal("Never", row.Cells[9]);
        Assert.Equal("05-01-2027", row.Cells[10]);
    }

    [Fact]
    public void A_risk_not_reviewed_for_12_months_is_marked_review_due()
    {
        var risk = NewRisk("Phishing", 2, 2);

        var row = RiskAssessmentReport.Row(risk, Names, Monday.AddMonths(13));

        Assert.Equal("Review due", row.Cells[10]);
        Assert.Equal(ReportStatus.Warning, row.Status);
    }

    [Fact]
    public void The_last_review_moves_the_next_one()
    {
        var risk = NewRisk("Phishing", 2, 2);
        risk.MarkReviewed(Anna, Monday.AddMonths(6));

        Assert.Equal(Monday.AddMonths(18), RiskAssessmentReport.NextReview(risk));
    }

    [Fact]
    public void An_accepted_risk_shows_who_accepted_it_and_their_role()
    {
        var risk = NewRisk("Phishing", 2, 2);
        risk.Accept("Training every year", Anna, Monday, UserRole.ComplianceManager);

        var row = Sheet(Build(risk), "Accepted risks").Single();

        Assert.Equal(["Phishing", "Low (4)", "Anna", "Compliance manager"], row.Cells.Take(4));
        Assert.Equal("Training every year", row.Cells[5]);
    }

    [Fact]
    public void Closed_risks_are_only_in_their_own_section()
    {
        var closed = NewRisk("Old TLS", 3, 3);
        closed.Close("API retired", Monday);

        var document = Build(closed, NewRisk("Phishing", 2, 2));

        Assert.Equal(["Phishing"], Sheet(document, "Risks").Select(r => r.Cells[0]));
        Assert.Equal(["Old TLS", "Medium (9)", "05-01-2026", "API retired"], Sheet(document, "Closed risks").Single().Cells);
    }

    [Fact]
    public void The_summary_counts_the_open_risks_by_level()
    {
        var summary = Build(NewRisk("A", 4, 5), NewRisk("B", 4, 5), NewRisk("C", 1, 1)).Summary.ToDictionary(s => s.Label, s => s.Value);

        Assert.Equal("2 Critical, 1 Low", summary["Open risks"]);
        Assert.Equal("Development", summary["Lifecycle phase"]);
    }

    [Fact]
    public void Without_risks_the_summary_says_none()
        => Assert.Equal("None", Build().Summary.Single(s => s.Label == "Open risks").Value);

    [Fact]
    public void A_residual_level_is_shown_when_it_is_set()
    {
        var risk = NewRisk("Cookie", 3, 4);
        risk.Update("Cookie", null, 3, 4, Anna, RiskTreatment.Reduce, 1, 2);

        Assert.Equal("Low (1 × 2)", RiskAssessmentReport.Row(risk, Names, Monday).Cells[3]);
    }
}
