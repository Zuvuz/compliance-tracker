using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Projects;

// Edge-case unit tests of US-119: the plain checks behind the Suggestions tab (R3: no AI) and dismissals (R5).
// Arrange / Act / Assert.
public class GapFinderTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static ProjectCompliance Compliance(params Func<FrameworkVersion, RequirementCoverage>[] rows)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA – Regulation (EU) 2024/2847", CatalogFile.Create("cra.pdf", bytes), Anna, Monday);
        return new ProjectCompliance([new FrameworkCompliance(framework, framework.LatestVersion, rows.Select(r => r(framework.LatestVersion)).ToList())], []);
    }

    private static WorkItem Story()
        => WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#1", "https://github.com/x/y/issues/1", "[US-001] Story", "", "", "", "", false), Monday);

    private static LinkedWorkItem Link(Requirement requirement)
    {
        var story = Story();
        return new LinkedWorkItem(RequirementLink.Create(Project, requirement.Id, story.Id, Anna, Monday), story);
    }

    private static Risk Risk(string title, RiskTreatment treatment = RiskTreatment.Reduce)
        => Domain.Risks.Risk.Create(Project, title, null, 3, 4, Anna, treatment, Anna, Monday);

    // ---------------------------------------------------------- Requirements

    [Fact]
    public void A_requirement_not_covered_and_one_without_evidence_are_gaps_a_covered_one_with_evidence_is_not()
    {
        // Arrange
        var compliance = Compliance(
            v => new RequirementCoverage(v.AddRequirement("I.2(a)", "Not covered", null), []),
            v => { var r = v.AddRequirement("I.2(b)", "No evidence", null); return new RequirementCoverage(r, [Link(r)]); },
            v =>
            {
                var r = v.AddRequirement("I.2(c)", "Done", null);
                return new RequirementCoverage(r, [Link(r)])
                {
                    Evidence = [new EvidenceItem(Evidence.ForLink(Project, "Proof", "https://example.org", null, Anna, null, Monday), false)]
                };
            });

        // Act
        var gaps = GapFinder.RequirementGaps(compliance).ToList();

        // Assert
        Assert.Equal(["Not covered by any user story", "Covered, but no evidence proves it"], gaps.Select(g => g.Missing));
        Assert.All(gaps, g => Assert.Equal((GapCategory.Requirements, GapAction.AskTheAi, "cra"), (g.Category, g.Action, g.Sub)));
    }

    [Fact]
    public void Not_applicable_requirements_and_recommendations_are_never_gaps()
    {
        // Arrange
        var compliance = Compliance(
            v => new RequirementCoverage(v.AddRequirement("I.2(a)", "Does not apply", null), [], "No network"),
            v => new RequirementCoverage(v.AddRequirement("5.1", "Policies", null, RequirementKind.Recommendation), []));

        // Act / Assert
        Assert.Empty(GapFinder.RequirementGaps(compliance));
    }

    // ---------------------------------------------------------- Risk

    [Fact]
    public void A_risk_to_reduce_without_actions_is_a_gap_an_accepted_one_is_not()
    {
        // Act
        var gaps = GapFinder.RiskGaps([Risk("To reduce"), Risk("Accepted", RiskTreatment.Accept)], Today).ToList();

        // Assert
        var gap = Assert.Single(gaps);
        Assert.Equal(("To reduce", "No treatment actions (High)", GapAction.AddAction), (gap.Title, gap.Missing, gap.Action));
    }

    [Fact]
    public void Overdue_actions_are_a_gap_that_changes_its_stamp_with_the_count()
    {
        // Arrange
        var risk = Risk("Late");
        risk.AddAction("First", Anna, Today.AddDays(-2), Anna, Monday);
        var before = GapFinder.RiskGaps([risk], Today).Single();
        risk.AddAction("Second", Anna, Today.AddDays(-1), Anna, Monday);

        // Act
        var after = GapFinder.RiskGaps([risk], Today).Single();

        // Assert
        Assert.Equal("1 overdue action", before.Missing);
        Assert.Equal("2 overdue actions", after.Missing);
        Assert.NotEqual(before.Stamp, after.Stamp);
    }

    [Fact]
    public void Every_action_done_without_a_residual_level_is_a_gap()
    {
        // Arrange
        var risk = Risk("Treated");
        var action = risk.AddAction("Fix", Anna, null, Anna, Monday);
        typeof(RiskAction).GetProperty(nameof(RiskAction.Status))!.SetValue(action, RiskActionStatus.Done);

        // Act
        var gap = Assert.Single(GapFinder.RiskGaps([risk], Today));

        // Assert
        Assert.Equal("Every action is done, but the residual level is not set", gap.Missing);
    }

    // ---------------------------------------------------------- Scanning

    [Fact]
    public void Only_open_high_and_critical_findings_are_gaps_highest_first()
    {
        // Arrange
        var scan = Guid.NewGuid();
        var findings = new[]
        {
            Finding.Create(Project, "CodeQL", "a", "sql-injection", FindingSeverity.High, "SQL injection in /search", "src/Search.cs", scan),
            Finding.Create(Project, "CodeQL", "b", "rce", FindingSeverity.Critical, "", null, scan),
            Finding.Create(Project, "CodeQL", "c", "style", FindingSeverity.Medium, "Long line", null, scan)
        };

        // Act
        var gaps = GapFinder.ScanningGaps(findings).ToList();

        // Assert
        Assert.Equal(["rce – (no message)", "sql-injection – SQL injection in /search"], gaps.Select(g => g.Title));
        Assert.Equal("High finding from CodeQL in src/Search.cs with no fix yet", gaps[1].Missing);
        Assert.All(gaps, g => Assert.Equal(GapAction.AddRisk, g.Action));
    }

    // ---------------------------------------------------------- Security

    [Fact]
    public void An_unsent_report_past_its_deadline_is_a_gap_a_sent_one_is_not()
    {
        // Arrange: aware 2 days ago, so the early warning (24 hours) is overdue and the notification (72 hours) is not yet
        var now = Monday;
        var exploited = SecurityEvent.Record(Project, SecurityEventKind.ActivelyExploitedVulnerability, "CVE-2026-1234", null, now.AddDays(-2), Anna, null, now);
        var sent = SecurityEvent.Record(Project, SecurityEventKind.ActivelyExploitedVulnerability, "CVE-2026-9999", null, now.AddDays(-2), Anna, null, now);
        sent.MarkSent(SecurityReportKind.EarlyWarning, now.AddDays(-1), "ENISA-1", now);

        // Act
        var gaps = GapFinder.SecurityGaps([exploited, sent], now).ToList();

        // Assert
        var gap = Assert.Single(gaps);
        Assert.Equal(("CVE-2026-1234", "Send the early warning (CRA Art. 14)"), (gap.Title, gap.Suggestion));
    }

    [Fact]
    public void A_closed_event_has_no_gaps()
    {
        // Arrange
        var closed = SecurityEvent.Record(Project, SecurityEventKind.ActivelyExploitedVulnerability, "Old", null, Monday.AddDays(-30), Anna, null, Monday);
        typeof(SecurityEvent).GetProperty(nameof(SecurityEvent.ClosedAt))!.SetValue(closed, Monday); // closing needs the reports sent first

        // Act / Assert
        Assert.Empty(GapFinder.SecurityGaps([closed], Monday));
    }

    // ---------------------------------------------------------- Dismissals

    [Fact]
    public void A_dismissal_hides_the_gap_only_while_its_source_is_unchanged()
    {
        // Act
        var dismissal = GapDismissal.Create(Project, "risk:1:overdue", "overdue:1", "Planned elsewhere", Anna, Monday);

        // Assert
        Assert.True(dismissal.Hides("risk:1:overdue", "overdue:1"));
        Assert.False(dismissal.Hides("risk:1:overdue", "overdue:2"));
        Assert.False(dismissal.Hides("risk:2:overdue", "overdue:1"));
    }

    [Theory]
    [InlineData("key", " ")]
    [InlineData(" ", "A reason")]
    public void A_dismissal_needs_a_key_and_a_reason(string key, string reason)
    {
        // Act / Assert
        Assert.Throws<Domain.Common.DomainException>(() => GapDismissal.Create(Project, key, "s", reason, Anna, Monday));
    }
}
