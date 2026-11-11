using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Edge-case unit tests of US-106 in the domain: the AI's story draft and its measure suggestions. Arrange / Act / Assert.
public class StoryDraftTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static StoryDraft Draft()
        => StoryDraft.Write(Guid.NewGuid(), Guid.NewGuid(), " Delete my account ", "User", "As a user, I want to delete my account.", "R1", "E1", "fake-chat", "story-draft-v1", Monday);

    [Fact]
    public void A_new_draft_is_open_and_trimmed()
    {
        // Act
        var draft = Draft();

        // Assert
        Assert.True(draft.IsOpen);
        Assert.Equal("Delete my account", draft.Title);
    }

    [Theory]
    [InlineData("", "A story")]
    [InlineData("A title", " ")]
    public void A_draft_needs_a_title_and_a_story(string title, string story)
    {
        // Act / Assert
        Assert.Equal("A draft needs a title and a user story",
            Assert.Throws<DomainException>(() => StoryDraft.Write(Guid.NewGuid(), Guid.NewGuid(), title, "", story, "", "", "m", "v", Monday)).Message);
    }

    [Fact]
    public void Accepting_with_changes_records_that_a_person_edited_it()
    {
        // Arrange
        var unchanged = Draft();
        var changed = Draft();

        // Act
        unchanged.Accept(unchanged.Title, unchanged.Role, unchanged.Story, unchanged.Rules, unchanged.Examples, Anna, Monday);
        changed.Accept(changed.Title, changed.Role, changed.Story, "R1: Every account can be deleted", changed.Examples, Anna, Monday);

        // Assert
        Assert.False(unchanged.EditedByPerson);
        Assert.True(changed.EditedByPerson);
        Assert.Equal(StoryDraftStatus.Accepted, changed.Status);
    }

    [Fact]
    public void A_draft_is_decided_once_and_discarding_needs_a_reason()
    {
        // Arrange
        var draft = Draft();

        // Act / Assert
        Assert.Equal("Give a reason for discarding the draft", Assert.Throws<DomainException>(() => draft.Discard(" ", Anna, Monday)).Message);
        draft.Discard("We do not store accounts", Anna, Monday);
        Assert.Throws<DomainException>(() => draft.Accept("t", "", "s", "", "", Anna, Monday));
    }

    [Fact]
    public void Only_an_accepted_draft_is_linked_to_a_story_and_only_once()
    {
        // Arrange
        var draft = Draft();
        var first = Guid.NewGuid();

        // Act / Assert
        Assert.Throws<DomainException>(() => draft.LinkedTo(first));
        draft.Accept(draft.Title, draft.Role, draft.Story, draft.Rules, draft.Examples, Anna, Monday);
        draft.LinkedTo(first);
        draft.LinkedTo(Guid.NewGuid());
        Assert.Equal(first, draft.WorkItemId);
    }

    [Fact]
    public void The_issue_body_has_the_templates_sections_and_says_it_was_drafted()
    {
        // Act
        var body = Draft().IssueBody("I.2(m)");

        // Assert
        Assert.Contains("### User Story", body);
        Assert.Contains("### Rules", body);
        Assert.Contains("Drafted by the AI for I.2(m)", body);
    }

    [Fact]
    public void A_measure_suggestion_is_a_requirement_or_a_story_never_both()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.7, "m", Monday));
        Assert.Throws<DomainException>(() => RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, 0.7, "m", Monday));
        var requirement = Guid.NewGuid();
        Assert.True(RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), requirement, null, 0.7, "m", Monday).IsFor(requirement, null));
    }

    [Fact]
    public void Rejecting_a_measure_needs_a_reason_and_replacing_keeps_decided_ones()
    {
        // Arrange
        var rejected = RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 0.7, "m", Monday);
        var pending = RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 0.7, "m", Monday);

        // Act
        Assert.Throws<DomainException>(() => rejected.Reject(null, null, Anna, Monday));
        rejected.Reject(RejectionReason.NotRelevant, null, Anna, Monday);
        rejected.Replace();
        pending.Replace();

        // Assert
        Assert.Equal((SuggestionStatus.Rejected, SuggestionStatus.Replaced), (rejected.Status, pending.Status));
    }
}
