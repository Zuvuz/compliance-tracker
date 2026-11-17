using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-200: the steps of following one requirement to the end, and the next requirement. Edge cases.
// Arrange / Act / Assert.
public class RequirementPathTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private const string EvidenceTab = "/Projects/Details/1/evidence";

    [Fact]
    public void Without_stories_the_first_step_is_to_link_one()
    {
        var steps = RequirementPath.Steps(new RequirementCoverage(Requirement(), []), [], EvidenceTab);

        Assert.Equal(["Link a user story that meets the requirement", "Tests pass", "Evidence on the requirement"], steps.Select(s => s.Text));
        Assert.Equal("link-story", steps[0].DialogOpen);
        Assert.Equal("0 of 3 steps done", RequirementPath.Progress(steps));
    }

    [Fact]
    public void Each_story_has_its_own_steps_so_all_can_be_finished()
    {
        var requirement = Requirement();
        var row = new RequirementCoverage(requirement, [Link(requirement, "Sign in"), Link(requirement, "Lock account")]);

        var texts = RequirementPath.Steps(row, [], EvidenceTab).Select(s => s.Text).ToList();

        Assert.Equal(["Sign in: done", "Sign in: evidence", "Lock account: done", "Lock account: evidence", "Tests pass", "Evidence on the requirement"], texts);
    }

    [Fact]
    public void A_closed_story_with_evidence_has_both_its_steps_done()
    {
        var requirement = Requirement();
        var link = Link(requirement, "Sign in", closed: true);
        var evidence = new StoryEvidence(link.WorkItem.Id, Evidence.ForLink(Guid.NewGuid(), "Screenshot", "https://example.com/s", null, Anna, null, Monday));

        var steps = RequirementPath.Steps(new RequirementCoverage(requirement, [link]), [evidence], EvidenceTab);

        Assert.True(steps.Single(s => s.Text == "Sign in: done").Done);
        Assert.True(steps.Single(s => s.Text == "Sign in: evidence").Done);
    }

    [Fact]
    public void Evidence_of_another_story_does_not_count()
    {
        var requirement = Requirement();
        var evidence = new StoryEvidence(Guid.NewGuid(), Evidence.ForLink(Guid.NewGuid(), "Screenshot", "https://example.com/s", null, Anna, null, Monday));

        var steps = RequirementPath.Steps(new RequirementCoverage(requirement, [Link(requirement, "Sign in")]), [evidence], EvidenceTab);

        Assert.False(steps.Single(s => s.Text == "Sign in: evidence").Done);
    }

    [Fact]
    public void An_open_story_links_out_to_its_source()
    {
        var requirement = Requirement();

        var done = RequirementPath.Steps(new RequirementCoverage(requirement, [Link(requirement, "Sign in")]), [], EvidenceTab)[0];

        Assert.False(done.Done);
        Assert.Equal("Still open in GitHub", done.Missing);
        Assert.StartsWith("https://github.com/", done.Href);
    }

    [Fact]
    public void A_link_waiting_for_review_gets_a_step_before_the_story_and_no_extra_review_step()
    {
        var requirement = Requirement();
        var link = Link(requirement, "Sign in");
        var carried = link with { Link = link.Link.CarryTo(requirement.Id, textChanged: true) };

        var texts = RequirementPath.Steps(new RequirementCoverage(requirement, [carried]), [], EvidenceTab).Select(s => s.Text).ToList();

        Assert.Equal("Sign in: confirm the link", texts[0]);
        Assert.DoesNotContain("Decide what waits for review", texts);
    }

    [Fact]
    public void Failing_tests_keep_the_test_step_open_and_say_how_many()
    {
        var requirement = Requirement();
        var blocked = EvidenceSuggestion.FromTestRun(Guid.NewGuid(), requirement.Id, "sha", "results.trx", ["a"], ["b", "c"], Monday);
        var row = new RequirementCoverage(requirement, []) { EvidenceSuggestions = [blocked] };

        var tests = RequirementPath.Steps(row, [], EvidenceTab).Single(s => s.Text == "Tests pass");

        Assert.False(tests.Done);
        Assert.Equal("2 failing tests", tests.Missing);
    }

    [Fact]
    public void Evidence_on_the_requirement_ticks_its_step_but_not_the_tests()
    {
        var row = new RequirementCoverage(Requirement(), []) { Evidence = [Item()] };

        var steps = RequirementPath.Steps(row, [], EvidenceTab);

        Assert.True(steps.Single(s => s.Text == "Evidence on the requirement").Done);
        Assert.False(steps.Single(s => s.Text == "Tests pass").Done);
    }

    [Fact]
    public void A_requirement_that_does_not_apply_has_no_steps_and_is_not_complete()
    {
        var steps = RequirementPath.Steps(new RequirementCoverage(Requirement(), [], "No remote sign-in"), [], EvidenceTab);

        Assert.Empty(steps);
        Assert.False(RequirementPath.Complete(steps));
    }

    [Fact]
    public void In_the_organisation_area_only_evidence_is_a_step()
    {
        var row = new RequirementCoverage(Requirement(), []) { EvidenceCovers = true, Evidence = [Item()] };

        var steps = RequirementPath.Steps(row, [], EvidenceTab);

        Assert.Equal("Evidence on the requirement", Assert.Single(steps).Text);
        Assert.True(RequirementPath.Complete(steps));
        Assert.Equal("1 of 1 step done", RequirementPath.Progress(steps));
    }

    [Fact]
    public void Next_skips_the_requirements_the_filter_hides_and_is_null_at_the_end()
    {
        var framework = Framework.Create(Guid.NewGuid(), "CRA", Catalog(), Anna, Monday);
        var a = framework.LatestVersion.AddRequirement("I.1", "A", null);
        var b = framework.LatestVersion.AddRequirement("I.2", "B", null);
        var c = framework.LatestVersion.AddRequirement("I.3", "C", null);
        var compliance = new FrameworkCompliance(framework, framework.LatestVersion,
            [new RequirementCoverage(a, []), new RequirementCoverage(b, [], "Not relevant"), new RequirementCoverage(c, [])]);

        Assert.Equal(b.Id, RequirementPath.Next(compliance, a.Id, ComplianceFilters.All)!.Requirement.Id);
        Assert.Equal(c.Id, RequirementPath.Next(compliance, a.Id, ComplianceFilters.NotCovered)!.Requirement.Id);
        Assert.Null(RequirementPath.Next(compliance, c.Id, ComplianceFilters.All));
        Assert.Null(RequirementPath.Next(compliance, Guid.NewGuid(), ComplianceFilters.All));
    }

    private static LinkedWorkItem Link(Requirement requirement, string title, bool closed = false)
    {
        var item = WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub,
            new ExternalWorkItem("#1", "https://github.com/x/y/issues/1", title, "", "", "", "user-story", closed), Monday);
        return new LinkedWorkItem(RequirementLink.Create(Guid.NewGuid(), requirement.Id, item.Id, Anna, Monday), item);
    }

    private static EvidenceItem Item()
        => new(Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/policy", null, Anna, null, Monday), false);

    private static CatalogFile Catalog()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return CatalogFile.Create("f.pdf", bytes);
    }

    private static Requirement Requirement()
        => Framework.Create(Guid.NewGuid(), "CRA", Catalog(), Anna, Monday).LatestVersion.AddRequirement("I.2(d)", "Sign in", null);
}
