using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Compliance;

// Edge-case unit tests of US-144: the texts of a requirement row (sources, a story's state, the summary). Arrange / Act / Assert.
public class RequirementRowsTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static string NameOf(Guid id) => id == Anna ? "Anna" : id == Bo ? "Bo" : "Unknown";

    private static Requirement Requirement()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("cra.pdf", bytes), Anna, Monday);
        return framework.LatestVersion.AddRequirement("I.2(d)", "Protection from unauthorised access", null);
    }

    private static WorkItem Story(bool closed = false)
        => WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Sign in", "", "", "", "user-story", closed), Monday);

    private static LinkedWorkItem Link(LinkSource source = LinkSource.Manual, bool closed = false)
        => new(RequirementLink.Create(Project, Guid.NewGuid(), Guid.NewGuid(), Bo, Monday, source), Story(closed));

    private static EvidenceItem Item(Evidence evidence, EvidenceSourceKind? from = null) => new(evidence, false) { AcceptedFrom = from };

    private static Evidence LinkEvidence() => Evidence.ForLink(Project, "PR 42", "https://github.com/x/y/pull/42", null, Anna, null, Monday);

    private static Evidence FileEvidence() => Evidence.ForFile(Project, "Policy", EvidenceFile.Create("policy.txt", Encoding.UTF8.GetBytes("text")), null, Anna, null, Monday);

    [Theory]
    [InlineData(false, "Open")]
    [InlineData(true, "Done")]
    public void A_finished_story_reads_done(bool closed, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, RequirementRows.StoryState(Story(closed).State));
    }

    [Fact]
    public void A_removed_story_says_so()
    {
        // Act / Assert
        Assert.Equal("Removed", RequirementRows.StoryState(WorkItemState.Removed));
    }

    [Theory]
    [InlineData(LinkSource.Manual, "GitHub · linked by Bo, 02-11-2026")]
    [InlineData(LinkSource.AiApproved, "GitHub · AI suggestion approved by Bo, 02-11-2026")]
    public void A_story_says_where_it_came_from_and_who_linked_it(LinkSource source, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, RequirementRows.StorySource(Link(source), NameOf));
    }

    [Fact]
    public void A_story_from_a_specification_says_so()
    {
        // Arrange
        var story = WorkItem.FromDocument(Project, Guid.NewGuid(), WorkItemType.Requirement, new DocumentWorkItem("REQ-1", "Log in"), 1, Monday);

        // Act
        var source = RequirementRows.StorySource(new LinkedWorkItem(RequirementLink.Create(Project, Guid.NewGuid(), story.Id, Anna, Monday), story), NameOf);

        // Assert
        Assert.Equal("Specification · linked by Anna, 02-11-2026", source);
    }

    [Fact]
    public void A_person_who_left_is_unknown()
    {
        // Arrange
        var link = new LinkedWorkItem(RequirementLink.Create(Project, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday), Story());

        // Act / Assert
        Assert.Contains("linked by Unknown", RequirementRows.StorySource(link, NameOf));
    }

    [Fact]
    public void Evidence_says_how_it_was_added()
    {
        // Act / Assert
        Assert.Equal("Link added by Anna, 02-11-2026", RequirementRows.EvidenceSource(Item(LinkEvidence()), NameOf));
        Assert.Equal("Uploaded by Anna, 02-11-2026", RequirementRows.EvidenceSource(Item(FileEvidence()), NameOf));
    }

    [Theory]
    [InlineData(EvidenceSourceKind.TestRun, "From a test run · accepted by Anna, 02-11-2026")]
    [InlineData(EvidenceSourceKind.PullRequest, "From a merged pull request · accepted by Anna, 02-11-2026")]
    public void Evidence_accepted_from_a_suggestion_names_its_origin(EvidenceSourceKind kind, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, RequirementRows.EvidenceSource(Item(LinkEvidence(), kind), NameOf));
    }

    [Fact]
    public void The_summary_counts_stories_and_evidence()
    {
        // Arrange
        var empty = new RequirementCoverage(Requirement(), []);
        var one = new RequirementCoverage(Requirement(), [Link()]) { Evidence = [Item(LinkEvidence()), Item(FileEvidence())] };
        var two = new RequirementCoverage(Requirement(), [Link(), Link()]);

        // Act / Assert
        Assert.Equal("0 stories · 0 evidence", RequirementRows.Summary(empty));
        Assert.Equal("1 story · 2 evidence", RequirementRows.Summary(one));
        Assert.Equal("2 stories · 0 evidence", RequirementRows.Summary(two));
    }

    [Fact]
    public void Nothing_waits_on_a_plain_row()
    {
        // Act / Assert
        Assert.Equal(0, RequirementRows.Waiting(new RequirementCoverage(Requirement(), [Link()])));
    }

    [Fact]
    public void A_not_applicable_row_has_no_waiting_ai_suggestions()
    {
        // Arrange: the suggestion lists are ignored when the requirement does not apply
        var row = new RequirementCoverage(Requirement(), [], "Not used");

        // Act / Assert
        Assert.Equal(0, RequirementRows.Waiting(row));
    }
}
