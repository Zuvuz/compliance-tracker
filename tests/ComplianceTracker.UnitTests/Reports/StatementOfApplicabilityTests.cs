using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Reports;

// Unit tests of the Statement of Applicability (US-053). Every test follows Arrange / Act / Assert.
public class StatementOfApplicabilityTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static Framework Iso(string name = "ISO/IEC 27001:2022", params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("iso.pdf", bytes), Anna, Monday);
        foreach (var code in codes.Length == 0 ? ["5.1", "A.5.15", "A.8.24"] : codes)
        {
            framework.LatestVersion.AddRequirement(code, $"Control {code}", null);
        }

        return framework;
    }

    private static readonly Framework Standard = Iso();

    private static LinkedWorkItem Story(string title)
        => new(RequirementLink.Create(ProjectId, Guid.NewGuid(), Guid.NewGuid(), Anna, Monday),
            WorkItem.Create(ProjectId, WorkItemSource.GitHub, new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", title, "", "", "", "user-story", false), Monday));

    private static EvidenceItem Evidence(string title, bool expired = false)
        => new(Domain.Mappings.Evidence.ForLink(ProjectId, title, "https://example.com/" + title, null, Anna, null, Monday), expired);

    // One project's coverage: per code, (not applicable reason, stories, evidence)
    private static SoaProject Project(string name, Dictionary<string, (string? Reason, string[] Stories, string[] Evidence)>? coverage = null)
    {
        var version = Standard.LatestVersion;
        var rows = version.Requirements.Select(r =>
        {
            var c = coverage is not null && coverage.TryGetValue(r.Code, out var found) ? found : (null, [], []);
            return new RequirementCoverage(r, c.Stories.Select(Story).ToList(), c.Reason) { Evidence = c.Evidence.Select(e => Evidence(e)).ToList() };
        }).ToList();
        return new SoaProject(name, new FrameworkCompliance(Standard, version, rows));
    }

    private static SoaControl Control(IReadOnlyList<SoaProject> projects, string code)
        => StatementOfApplicability.Controls(projects).Single(c => c.Code == code);

    [Fact]
    public void Only_Annex_A_is_listed_when_the_standard_has_it_in_its_order()
        => Assert.Equal(["A.5.15", "A.8.24"], StatementOfApplicability.Controls([Project("Portal")]).Select(c => c.Code));

    [Fact]
    public void Without_Annex_A_codes_every_requirement_is_listed()
    {
        var other = Iso("ISO 27001", "5.1", "6.1");
        var project = new SoaProject("Portal", new FrameworkCompliance(other, other.LatestVersion,
            other.LatestVersion.Requirements.Select(r => new RequirementCoverage(r, [])).ToList()));

        Assert.Equal(["5.1", "6.1"], StatementOfApplicability.Controls([project]).Select(c => c.Code));
    }

    [Fact]
    public void No_projects_give_no_controls()
        => Assert.Empty(StatementOfApplicability.Controls([]));

    [Fact]
    public void An_uncovered_control_is_applicable_and_not_implemented()
        => Assert.Equal((true, StatementOfApplicability.NotImplemented), (Control([Project("Portal")], "A.5.15").Applicable, Control([Project("Portal")], "A.5.15").Implementation));

    [Fact]
    public void A_control_covered_without_evidence_is_partly_implemented_and_names_the_story()
    {
        var control = Control([Project("Portal", new() { ["A.5.15"] = (null, ["[US-004] Sign in"], []) })], "A.5.15");

        Assert.Equal(StatementOfApplicability.Partly, control.Implementation);
        Assert.Equal(["Portal: [US-004] Sign in"], control.HowMet);
    }

    [Fact]
    public void Covered_with_evidence_in_every_project_is_implemented()
    {
        var covered = new Dictionary<string, (string?, string[], string[])> { ["A.5.15"] = (null, ["[US-004] Sign in"], ["Access review"]) };

        var control = Control([Project("Portal", covered), Project("App", covered)], "A.5.15");

        Assert.Equal(StatementOfApplicability.Implemented, control.Implementation);
        Assert.Equal(["Portal: [US-004] Sign in, Access review", "App: [US-004] Sign in, Access review"], control.HowMet);
    }

    [Fact]
    public void One_project_without_coverage_makes_it_partly()
    {
        var covered = new Dictionary<string, (string?, string[], string[])> { ["A.5.15"] = (null, ["[US-004] Sign in"], ["Access review"]) };

        Assert.Equal(StatementOfApplicability.Partly, Control([Project("Portal", covered), Project("App")], "A.5.15").Implementation);
    }

    [Fact]
    public void Not_applicable_in_one_project_keeps_it_applicable_with_the_reason()
    {
        var control = Control([Project("Portal", new() { ["A.8.24"] = ("No cryptography", [], []) }), Project("App")], "A.8.24");

        Assert.True(control.Applicable);
        Assert.Equal(["Portal: No cryptography"], control.NotApplicable);
        Assert.Equal(StatementOfApplicability.NotImplemented, control.Implementation);
    }

    [Fact]
    public void Not_applicable_everywhere_makes_it_not_applicable()
    {
        var control = Control([Project("Portal", new() { ["A.8.24"] = ("No cryptography", [], []) }),
            Project("App", new() { ["A.8.24"] = ("Stores nothing", [], []) })], "A.8.24");

        Assert.False(control.Applicable);
        Assert.Equal(StatementOfApplicability.NotApplicableText, control.Implementation);
        Assert.Equal(2, control.NotApplicable.Count);
    }

    [Fact]
    public void A_project_where_it_does_not_apply_does_not_lower_the_implementation()
    {
        var covered = new Dictionary<string, (string?, string[], string[])> { ["A.8.24"] = (null, ["[US-009] Encrypt"], ["TLS config"]) };

        var control = Control([Project("Portal", covered), Project("App", new() { ["A.8.24"] = ("Stores nothing", [], []) })], "A.8.24");

        Assert.Equal(StatementOfApplicability.Implemented, control.Implementation);
    }

    [Theory]
    [InlineData("ISO/IEC 27001:2022", true)]
    [InlineData("iso 27001", true)]
    [InlineData("ISO/IEC 27002", false)]
    [InlineData("Security policy", false)]
    public void The_standard_is_found_by_its_name(string name, bool expected)
        => Assert.Equal(expected, StatementOfApplicability.IsIso27001(Iso(name)));

    [Fact]
    public void The_report_counts_applicable_and_implemented_controls()
    {
        var organization = Domain.Organizations.Organization.Create("Acme ApS", "12345678", Domain.Organizations.Sector.DigitalInfrastructure,
            Domain.Organizations.OrganizationSize.Small, new Domain.Users.UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var portal = Project("Portal", new() { ["A.8.24"] = ("No cryptography", [], []) });

        var summary = StatementOfApplicability.Build(organization, Standard, [portal], "Anna", Monday).Summary.ToDictionary(s => s.Label, s => s.Value);

        Assert.Equal("2: 1 applicable, 1 not applicable", summary["Controls"]);
        Assert.Equal("0 implemented, 0 partly, 1 not implemented", summary["Implementation"]);
        Assert.Equal("ISO/IEC 27001:2022, version 1", summary["Standard"]);
    }
}
