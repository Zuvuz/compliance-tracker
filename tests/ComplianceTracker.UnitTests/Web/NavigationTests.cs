using ComplianceTracker.Application.Review;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Controllers;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-169: edge cases of the project tab groups and the "To review" inbox. Arrange / Act / Assert.
public class NavigationTests
{
    private static Domain.Projects.Project Project(bool organisationArea = false)
        => organisationArea
            ? Domain.Projects.Project.CreateOrganisationArea(Guid.NewGuid())
            : Domain.Projects.Project.Create(Guid.NewGuid(), "Acme Portal", null, "A portal", null, Domain.Projects.LifecyclePhase.Planning);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_project_has_at_most_six_groups_and_every_tab_is_in_one(bool organisationArea)
    {
        var project = Project(organisationArea);

        var groups = ProjectTabs.GroupsFor(project);

        Assert.True(groups.Count <= 6);
        Assert.Equal(ProjectTabs.For(project).Select(t => t.Slug).Order(), groups.SelectMany(g => g.Tabs).Select(t => t.Slug).Order());
    }

    [Fact]
    public void The_requirements_group_holds_compliance_link_product_requirements_and_frameworks_in_that_order() // US-220: Link after Compliance
        => Assert.Equal([ProjectTabs.Compliance, ProjectTabs.Link, ProjectTabs.ProductRequirements, ProjectTabs.Frameworks],
            ProjectTabs.GroupsFor(Project()).Single(g => g.Title == "Requirements").Tabs.Select(t => t.Slug));

    [Fact]
    public void Every_known_tab_belongs_to_exactly_one_group()
    {
        var all = ProjectTabs.All.Select(t => t.Slug).Concat(ProjectTabs.OrganisationArea.Select(t => t.Slug)).Distinct();

        Assert.All(all, slug => Assert.Single(ProjectTabs.Groups, g => g.Slugs.Contains(slug)));
    }

    [Theory]
    [InlineData(ReviewKind.StorySuggestions, Permission.DecideCompliance)]
    [InlineData(ReviewKind.NeedsAction, Permission.DecideCompliance)]
    [InlineData(ReviewKind.RiskDrafts, Permission.ManageRisks)]
    [InlineData(ReviewKind.EvidenceSuggestions, Permission.LinkWork)]
    public void Each_kind_needs_the_right_that_decides_it(ReviewKind kind, Permission right)
        => Assert.Equal(right, ReviewKinds.RightFor(kind));

    [Theory]
    [InlineData(ReviewKind.StorySuggestions, 1, "1 AI story suggestion")]
    [InlineData(ReviewKind.StorySuggestions, 3, "3 AI story suggestions")]
    [InlineData(ReviewKind.Approvals, 2, "2 approvals")]
    public void Each_line_says_how_many_and_what(ReviewKind kind, int count, string text)
        => Assert.Equal(text, ReviewKinds.Title(kind, count));

    [Fact]
    public void Every_kind_has_a_place_where_it_is_decided()
        => Assert.All(Enum.GetValues<ReviewKind>(), kind => Assert.False(string.IsNullOrEmpty(ReviewController.WhereDecided(kind).Tab)));

    [Fact]
    public void Needs_action_opens_the_overview_filtered_to_it()
        => Assert.Equal((ProjectTabs.Compliance, ComplianceFilters.NeedsAction), ReviewController.WhereDecided(ReviewKind.NeedsAction));
}
