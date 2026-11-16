using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// #52: the Compliance tab's filter "Without evidence": edge cases of which requirements it shows.
// Arrange / Act / Assert.
public class WithoutEvidenceFilterTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void A_requirement_with_nothing_is_without_evidence()
        => Assert.True(Shows(Row()));

    [Fact]
    public void A_requirement_with_evidence_that_counts_is_not_shown()
        => Assert.False(Shows(Row() with { Evidence = [Item()] }));

    [Fact]
    public void A_requirement_covered_by_a_story_but_without_evidence_is_shown()
    {
        var requirement = Requirement();
        var link = new LinkedWorkItem(RequirementLink.Create(Guid.NewGuid(), requirement.Id, Guid.NewGuid(), Anna, Monday),
            WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub, new ExternalWorkItem("#1", "https://github.com/x/y/issues/1", "Story", "", "", "", "user-story", false), Monday));
        var row = new RequirementCoverage(requirement, [link]);

        Assert.True(row.IsCovered);
        Assert.True(Shows(row));
    }

    [Fact]
    public void Expired_evidence_does_not_count()
        => Assert.True(Shows(Row() with { Evidence = [Item(expired: true)] }));

    [Fact]
    public void Evidence_waiting_for_review_does_not_count()
        => Assert.True(Shows(Row() with { Evidence = [Item() with { NeedsReview = true }] }));

    [Fact]
    public void A_requirement_that_does_not_apply_is_not_shown()
        => Assert.False(Shows(new RequirementCoverage(Requirement(), [], "No remote sign-in")));

    [Fact]
    public void A_recommendation_is_not_shown()
        => Assert.False(Shows(new RequirementCoverage(Requirement(RequirementKind.Recommendation), [])));

    [Fact]
    public void The_filter_is_found_whatever_the_case_and_has_a_button()
    {
        Assert.Equal(ComplianceFilters.WithoutEvidence, ComplianceFilters.Find("WITHOUT-EVIDENCE"));
        Assert.Contains((ComplianceFilters.WithoutEvidence, "Without evidence"), ComplianceFilters.Each);
    }

    private static bool Shows(RequirementCoverage row) => ComplianceFilters.Matches(ComplianceFilters.WithoutEvidence, row);

    private static RequirementCoverage Row() => new(Requirement(), []);

    private static EvidenceItem Item(bool expired = false)
        => new(Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/policy", null, Anna, null, Monday), expired);

    private static Requirement Requirement(RequirementKind kind = RequirementKind.Requirement)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        return framework.LatestVersion.AddRequirement("I.2(d)", "Sign in", null, kind);
    }
}
