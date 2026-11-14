using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Compliance;

// Edge-case unit tests of US-153: finding the requirement a page's address points at, the code a form sends back,
// and the status the overview row and the page show. Arrange / Act / Assert.
public class RequirementPagesTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static FrameworkCompliance Framework(string name, params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Domain.Frameworks.Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var rows = codes.Select(c => new RequirementCoverage(framework.LatestVersion.AddRequirement(c, "Title " + c, null), [])).ToList();
        return new FrameworkCompliance(framework, framework.LatestVersion, rows);
    }

    private static ProjectCompliance Compliance(params FrameworkCompliance[] frameworks) => new(frameworks, []);

    private static LinkedWorkItem Link(bool needsReview = false)
    {
        var link = RequirementLink.Create(Project, Guid.NewGuid(), Guid.NewGuid(), Anna, Monday);
        var story = WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Sign in", "", "", "", "user-story", false), Monday);
        return new LinkedWorkItem(needsReview ? link.CarryTo(Guid.NewGuid(), textChanged: true) : link, story);
    }

    // ---------------------------------------------------------- The code a form sends back (R4)

    [Theory]
    [InlineData("I.2(d)", "I.2(d)")]
    [InlineData("  A.5.1 ", "A.5.1")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void The_code_is_trimmed_and_a_blank_one_is_none(string? code, string? expected)
        => Assert.Equal(expected, RequirementPages.Clean(code));

    [Fact]
    public void A_code_longer_than_a_code_can_be_is_none()
    {
        Assert.NotNull(RequirementPages.Clean(new string('A', RequirementPages.MaxCodeLength)));
        Assert.Null(RequirementPages.Clean(new string('A', RequirementPages.MaxCodeLength + 1)));
    }

    // ---------------------------------------------------------- Finding the requirement (R3)

    [Fact]
    public void The_address_finds_the_requirement_in_its_framework_whatever_the_case()
    {
        var cra = Framework("CRA – Regulation (EU) 2024/2847", "I.2(d)", "I.2(e)");
        var sub = FrameworkNames.Slug(cra.Framework.Name);

        var found = RequirementPages.Find(Compliance(cra), sub, "i.2(D)");

        Assert.Equal("I.2(d)", found?.Row.Requirement.Code);
        Assert.Same(cra, found?.Framework);
    }

    [Fact]
    public void An_unknown_code_or_framework_or_no_compliance_finds_nothing()
    {
        var cra = Framework("CRA – Regulation (EU) 2024/2847", "I.2(d)");
        var sub = FrameworkNames.Slug(cra.Framework.Name);

        Assert.Null(RequirementPages.Find(Compliance(cra), sub, "X.99"));
        Assert.Null(RequirementPages.Find(Compliance(cra), "no-such-framework", "I.2(d)"));
        Assert.Null(RequirementPages.Find(null, sub, "I.2(d)"));
        Assert.Null(RequirementPages.Find(Compliance(cra), sub, "  "));
    }

    [Fact]
    public void The_same_code_in_two_frameworks_is_found_in_the_one_the_address_names()
    {
        var first = Framework("ISO/IEC 27001:2022", "5.1");
        var second = Framework("ISO/IEC 27002:2022", "5.1");

        var found = RequirementPages.Find(Compliance(first, second), FrameworkNames.Slug(second.Framework.Name), "5.1");

        Assert.Same(second, found?.Framework);
    }

    [Theory]
    [InlineData("all")]
    [InlineData(null)]
    public void All_or_no_sub_tab_looks_in_every_framework(string? sub)
    {
        var cra = Framework("CRA – Regulation (EU) 2024/2847", "I.2(d)");
        var aiAct = Framework("AI Act – Regulation (EU) 2024/1689", "Art. 9");

        Assert.Same(aiAct, RequirementPages.Find(Compliance(cra, aiAct), sub, "Art. 9")?.Framework);
    }

    // ---------------------------------------------------------- The status (R1)

    [Fact]
    public void A_requirement_without_links_is_not_covered()
    {
        var row = Framework("CRA", "I.2(d)").Requirements[0];

        Assert.Equal(("Not covered", "text-bg-danger"), (RequirementPages.Status(row), RequirementPages.StatusClass(row)));
    }

    [Fact]
    public void A_linked_requirement_is_covered()
    {
        var row = Framework("CRA", "I.2(d)").Requirements[0] with { Links = [Link()] };

        Assert.Equal(("Covered", "text-bg-success"), (RequirementPages.Status(row), RequirementPages.StatusClass(row)));
    }

    [Fact]
    public void Only_links_that_wait_for_review_give_covered_needs_review()
    {
        var row = Framework("CRA", "I.2(d)").Requirements[0] with { Links = [Link(needsReview: true)] };

        Assert.Equal(("Covered, needs review", "text-bg-warning"), (RequirementPages.Status(row), RequirementPages.StatusClass(row)));
    }

    [Fact]
    public void Not_applicable_wins_over_links()
    {
        var row = Framework("CRA", "I.2(d)").Requirements[0] with { Links = [Link()], NotApplicableReason = "No remote access" };

        Assert.Equal(("Not applicable", "text-bg-secondary"), (RequirementPages.Status(row), RequirementPages.StatusClass(row)));
    }
}
