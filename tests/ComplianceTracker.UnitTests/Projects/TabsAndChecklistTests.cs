using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Projects;

// US-152: edge-case unit tests of US-022 (the project page's tabs) and US-096 (the "Get started" checklist).
// Arrange / Act / Assert.
public class TabsAndChecklistTests
{
    // ---------------------------------------------------------- US-022 Tabs

    [Theory]
    [InlineData(null, "overview")]
    [InlineData("", "overview")]
    [InlineData("RISKS", "risks")]
    [InlineData("product-requirements", "product-requirements")]
    public void An_address_finds_its_tab_and_no_tab_is_the_overview(string? slug, string expected)
        => Assert.Equal(expected, ProjectTabs.Find(slug));

    [Theory]
    [InlineData("nope")]
    [InlineData("nis2-scope")]   // only the organisation area has it
    [InlineData("user-stories")] // the old address is redirected before this
    [InlineData(" risks")]
    public void An_unknown_tab_or_one_the_page_does_not_have_is_not_found(string slug)
        => Assert.Null(ProjectTabs.Find(slug));

    [Theory]
    [InlineData("nis2-scope", "nis2-scope")]
    [InlineData("suggestions", null)]
    [InlineData("scans", null)]
    public void The_organisation_area_has_its_own_tabs(string slug, string? expected)
        => Assert.Equal(expected, ProjectTabs.Find(slug, organisationArea: true));

    [Fact]
    public void Every_tab_has_one_address_and_a_title_and_the_overview_comes_first()
    {
        foreach (var tabs in new[] { ProjectTabs.All, ProjectTabs.OrganisationArea })
        {
            Assert.Equal(tabs.Count, tabs.Select(t => t.Slug).Distinct().Count());
            Assert.All(tabs, t => Assert.False(string.IsNullOrWhiteSpace(t.Title)));
            Assert.Equal(ProjectTabs.Overview, tabs[0].Slug);
        }
    }

    // ---------------------------------------------------------- US-096 Get started

    private static GetStartedItem Step(GetStartedStep step, bool done) => new(step, step.ToString(), done, null);

    [Fact]
    public void The_card_shows_while_a_step_is_to_do_and_the_member_has_not_hidden_it()
    {
        var checklist = new GetStartedChecklist([Step(GetStartedStep.CreateOrganisation, true), Step(GetStartedStep.CreateProject, false)], Hidden: false, null);

        Assert.True(checklist.Show);
        Assert.False(checklist.AllDone);
    }

    [Fact]
    public void A_hidden_card_is_not_shown_even_with_steps_to_do()
        => Assert.False(new GetStartedChecklist([Step(GetStartedStep.CreateProject, false)], Hidden: true, null).Show);

    [Fact]
    public void When_every_step_is_done_the_card_goes_away_whether_hidden_or_not()
    {
        var done = new GetStartedChecklist([Step(GetStartedStep.CreateOrganisation, true), Step(GetStartedStep.AddEvidence, true)], Hidden: false, Guid.NewGuid());

        Assert.True(done.AllDone);
        Assert.False(done.Show);
    }

    [Fact]
    public void An_empty_checklist_counts_as_done()
        => Assert.False(new GetStartedChecklist([], Hidden: false, null).Show);
}
