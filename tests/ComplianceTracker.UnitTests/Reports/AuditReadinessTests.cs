using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Reports;

// US-178: edge cases of the audit package's check. Arrange / Act / Assert.
public class AuditReadinessTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly ReadinessFacts Nothing = new(false, null, null, 0, null);
    private static readonly ReadinessFacts Everything = new(true, new DateOnly(2031, 12, 31), "security@acme.example", 4, Today.AddMonths(-2));

    private static Framework NewFramework(string name, bool builtIn)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        if (builtIn)
        {
            typeof(Framework).GetProperty(nameof(Framework.OwnerOrganizationId))!.SetValue(framework, null);
        }

        return framework;
    }

    private static FrameworkCompliance Compliance(Framework framework, params RequirementCoverage[] rows)
        => new(framework, framework.LatestVersion, rows);

    private static RequirementCoverage Row(Framework framework, string code, string? notApplicable = null, params EvidenceItem[] evidence)
        => new(framework.LatestVersion.AddRequirement(code, "Title " + code, null), [], notApplicable) { Evidence = evidence };

    private static EvidenceItem Valid(DateOnly? until = null)
        => new(Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/p", until, Anna, null, Monday), false);

    private static EvidenceItem Expired()
        => new(Evidence.ForLink(Guid.NewGuid(), "Old policy", "https://example.com/o", Today.AddDays(-1), Anna, null, Monday), true);

    // ---------------------------------------------------------- Every framework (R1, R5)

    [Fact]
    public void A_framework_where_every_applicable_requirement_has_valid_evidence_is_ready()
    {
        var f = NewFramework("Security policy", builtIn: false);

        var check = AuditReadiness.Check([Compliance(f, Row(f, "SP-1", null, Valid()), Row(f, "SP-2", "Not used"))], Nothing, Today);

        Assert.True(check.IsReady);
        Assert.Equal("Ready", check.Summary);
    }

    [Fact]
    public void Requirements_without_valid_evidence_are_named_and_expired_evidence_does_not_count()
    {
        var f = NewFramework("Security policy", builtIn: false);

        var line = AuditReadiness.Check(Compliance(f, Row(f, "SP-1", null, Expired()), Row(f, "SP-2")), Nothing, Today).Lines.Single();

        Assert.False(line.Ok);
        Assert.Equal("2 without valid evidence: SP-1, SP-2", line.Detail);
    }

    [Fact]
    public void More_than_ten_missing_requirements_are_counted_after_the_first_ten()
    {
        var f = NewFramework("Security policy", builtIn: false);
        var rows = Enumerable.Range(1, 13).Select(i => Row(f, $"SP-{i}")).ToArray();

        var line = AuditReadiness.Check(Compliance(f, rows), Nothing, Today).Lines.Single();

        Assert.EndsWith("and 3 more", line.Detail);
    }

    [Fact]
    public void Evidence_that_ends_within_sixty_days_is_flagged_and_later_evidence_is_not()
    {
        var f = NewFramework("Security policy", builtIn: false);

        var lines = AuditReadiness.Check(Compliance(f, Row(f, "SP-1", null, Valid(Today.AddDays(30))), Row(f, "SP-2", null, Valid(Today.AddDays(90)))), Nothing, Today).Lines;

        var soon = Assert.Single(lines, l => l.Detail.StartsWith("Valid until", StringComparison.Ordinal));
        Assert.Equal("SP-1", soon.Code);
    }

    // ---------------------------------------------------------- ISO/IEC 27001 (R1)

    [Fact]
    public void Iso_key_clauses_without_evidence_are_missing_and_link_to_their_requirement()
    {
        var f = NewFramework("ISO/IEC 27001:2022", builtIn: false);

        var lines = AuditReadiness.Check(Compliance(f, Row(f, "4.3", null, Valid()), Row(f, "9.3")), Nothing, Today).Lines;

        Assert.True(lines.Single(l => l.Title == "Scope of the ISMS (4.3)").Ok);
        var review = lines.Single(l => l.Title == "Management review (9.3)");
        Assert.Equal((false, ReadinessFix.Requirement, "9.3"), (review.Ok, review.Fix, review.Code));
    }

    [Fact]
    public void An_internal_audit_held_within_twelve_months_covers_clause_9_2()
    {
        var f = NewFramework("ISO/IEC 27001:2022", builtIn: false);
        var g = NewFramework("ISO/IEC 27001:2022", builtIn: false);

        var recent = AuditReadiness.Check(Compliance(f, Row(f, "9.2")), Nothing with { LastAudit = Today.AddMonths(-3) }, Today).Lines.First();
        var old = AuditReadiness.Check(Compliance(g, Row(g, "9.2")), Nothing with { LastAudit = Today.AddMonths(-13) }, Today).Lines.First();

        Assert.True(recent.Ok);
        Assert.Equal((false, ReadinessFix.Audits), (old.Ok, old.Fix));
    }

    [Fact]
    public void A_not_applicable_key_clause_is_not_missing_and_says_why()
    {
        var f = NewFramework("ISO/IEC 27001:2022", builtIn: false);

        var line = AuditReadiness.Check(Compliance(f, Row(f, "9.3", "Done by the parent company")), Nothing, Today).Lines.First();

        Assert.Equal((true, "Not applicable: Done by the parent company"), (line.Ok, line.Detail));
    }

    [Fact]
    public void Key_clauses_the_organisations_copy_does_not_have_are_left_out()
        => Assert.DoesNotContain(AuditReadiness.Check(Compliance(NewFramework("ISO/IEC 27001:2022", builtIn: false)), Nothing, Today).Lines,
            l => l.Title.Contains("(4.3)", StringComparison.Ordinal));

    // ---------------------------------------------------------- CRA (R1)

    [Fact]
    public void The_cra_asks_for_an_sbom_a_support_period_a_contact_and_a_risk_assessment()
    {
        var cra = NewFramework("CRA – Regulation (EU) 2024/2847", builtIn: true);

        var missing = AuditReadiness.Check(Compliance(cra), Nothing, Today).Lines.Where(l => !l.Ok).Select(l => l.Fix).ToList();
        var present = AuditReadiness.Check(Compliance(cra), Everything, Today).Lines;

        Assert.Equal([ReadinessFix.Scans, ReadinessFix.ProjectEdit, ReadinessFix.ProjectEdit, ReadinessFix.Risks], missing.Take(4));
        Assert.All(present, l => Assert.True(l.Ok));
    }

    [Fact]
    public void An_uploaded_framework_named_cra_gets_no_cra_items()
        => Assert.DoesNotContain(AuditReadiness.Check(Compliance(NewFramework("CRA notes", builtIn: false)), Nothing, Today).Lines,
            l => l.Title.StartsWith("Software bill of materials", StringComparison.Ordinal));

    // ---------------------------------------------------------- The text in the package (R3)

    [Fact]
    public void The_text_marks_what_is_there_and_what_is_missing()
    {
        var f = NewFramework("Security policy", builtIn: false);
        var check = AuditReadiness.Check([Compliance(f, Row(f, "SP-1"))], Nothing, Today);

        var text = AuditReadiness.Text(check, Today);

        Assert.Contains("Audit package check, 16-11-2026: 1 thing is missing", text);
        Assert.Contains("  [ ] Evidence for every applicable requirement: 1 without valid evidence: SP-1", text);
    }
}
