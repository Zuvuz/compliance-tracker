using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Ai;

// US-155: edge cases of reading the AI's answer, choosing what one press checks, the status and the texts.
// Arrange / Act / Assert.
public class NeedsActionTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    // ---------------------------------------------------------- The answer (R2)

    [Theory]
    [InlineData("NEEDS ACTION HIGH: the product has remote sign-in", NeedsActionPriority.High, "the product has remote sign-in")]
    [InlineData("needs action medium:  logs are kept", NeedsActionPriority.Medium, "logs are kept")]
    [InlineData("\"NEEDS ACTION LOW: maybe\"\nmore text", NeedsActionPriority.Low, "maybe")]
    public void A_needs_action_line_gives_the_priority_and_the_reason(string answer, NeedsActionPriority priority, string reason)
        => Assert.Equal(new NeedsActionAnswer.NeedsAction(priority, reason), NeedsActionPrompt.Read(answer));

    [Fact]
    public void A_not_applicable_line_gives_the_reason()
        => Assert.Equal(new NeedsActionAnswer.DoesNotApply("no remote access"), NeedsActionPrompt.Read("NOT APPLICABLE: no remote access"));

    [Theory]
    [InlineData("UNSURE")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("NEEDS ACTION URGENT: now")]   // an unknown priority
    [InlineData("NEEDS ACTION HIGH")]          // no reason
    [InlineData("NEEDS ACTION HIGH:   ")]      // an empty reason
    [InlineData("NOT APPLICABLE:")]            // an empty reason
    [InlineData("I think it needs action")]    // not in the asked shape
    public void Anything_else_marks_nothing(string? answer)
        => Assert.Null(NeedsActionPrompt.Read(answer));

    [Fact]
    public void The_prompt_has_its_own_marker_so_it_is_never_taken_for_the_applicability_prompt()
        => Assert.DoesNotContain(ApplicabilityPrompt.Marker, NeedsActionPrompt.Marker, StringComparison.Ordinal);

    // ---------------------------------------------------------- What one press checks (R1)

    private static Requirement[] Requirements(params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        return codes.Select(c => framework.LatestVersion.AddRequirement(c, "Title " + c, null)).ToArray();
    }

    private static readonly HashSet<Guid> None = [];

    [Fact]
    public void Linked_not_applicable_suggested_and_marked_requirements_are_left_out()
    {
        var r = Requirements("A", "B", "C", "D", "E");

        var picked = NeedsActionTargets.Pick(r, new HashSet<Guid> { r[0].Id }, new HashSet<Guid> { r[1].Id }, new HashSet<Guid> { r[2].Id }, new HashSet<Guid> { r[3].Id }, 20);

        Assert.Equal(["E"], picked.Select(p => p.Code));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public void At_most_the_limit_is_checked(int limit, int expected)
        => Assert.Equal(expected, NeedsActionTargets.Pick(Requirements("A", "B", "C"), None, None, None, None, limit).Count);

    // ---------------------------------------------------------- The status (R3, R5)

    private static RequirementCoverage Row(NeedsActionSuggestion? mark, bool linked = false, string? notApplicable = null)
    {
        var requirement = Requirements("I.2(d)")[0];
        var links = linked
            ? new[] { new LinkedWorkItem(RequirementLink.Create(Guid.NewGuid(), requirement.Id, Guid.NewGuid(), Anna, Monday),
                WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub, new ExternalWorkItem("#1", "https://github.com/x/y/issues/1", "Story", "", "", "", "user-story", false), Monday)) }
            : [];
        return new RequirementCoverage(requirement, links, notApplicable) { NeedsActionMark = mark };
    }

    private static NeedsActionSuggestion Mark()
        => NeedsActionSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "remote sign-in", NeedsActionPriority.High, false, "", "m", "v", Monday);

    [Fact]
    public void A_marked_requirement_without_work_needs_action_and_waits()
    {
        var row = Row(Mark());

        Assert.Equal(("Needs action", 1), (RequirementPages.Status(row), RequirementRows.Waiting(row)));
    }

    [Fact]
    public void A_covered_or_not_applicable_requirement_no_longer_needs_action()
    {
        Assert.Equal("Covered", RequirementPages.Status(Row(Mark(), linked: true)));
        Assert.Null(Row(Mark(), linked: true).NeedsAction);
        Assert.Equal("Not applicable", RequirementPages.Status(Row(Mark(), notApplicable: "No IdP")));
    }

    [Fact]
    public void A_confirmed_mark_still_needs_action_but_no_longer_waits()
    {
        var mark = Mark();
        mark.Confirm(Anna, Monday, null);

        var row = Row(mark);

        Assert.Equal(("Needs action", 0), (RequirementPages.Status(row), RequirementRows.Waiting(row)));
    }

    [Fact]
    public void The_needs_action_filter_shows_only_marked_requirements()
    {
        Assert.True(ComplianceFilters.Matches(ComplianceFilters.NeedsAction, Row(Mark())));
        Assert.False(ComplianceFilters.Matches(ComplianceFilters.NeedsAction, Row(null)));
        Assert.Equal(ComplianceFilters.NeedsAction, ComplianceFilters.Find("NEEDS-ACTION"));
    }

    // ---------------------------------------------------------- The texts

    [Theory]
    [InlineData(0, 0, "The AI found nothing new that needs action")]
    [InlineData(1, 0, "1 requirement needs action. Decide on each one.")]
    [InlineData(3, 1, "3 requirements need action and 1 does not seem to apply. Decide on each one.")]
    [InlineData(0, 2, "2 do not seem to apply. Decide on each one.")]
    public void The_message_says_what_was_found(int needs, int notApplying, string expected)
        => Assert.Equal(expected, NeedsActionTexts.Found(needs, notApplying));

    [Theory]
    [InlineData(NeedsActionPriority.High, "text-bg-danger")]
    [InlineData(NeedsActionPriority.Medium, "text-bg-warning")]
    [InlineData(NeedsActionPriority.Low, "text-bg-secondary")]
    public void Each_priority_has_its_colour(NeedsActionPriority priority, string css)
        => Assert.Equal(css, NeedsActionTexts.PriorityClass(priority));
}
