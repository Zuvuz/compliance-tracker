using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Edge-case unit tests of US-146: evidence linked to a user story, and the AI's story suggestions. Arrange / Act / Assert.
public class EvidenceStoryLinkTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static EvidenceStorySuggestion Suggestion(double score = 0.7, int rank = 1)
        => EvidenceStorySuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), score, rank, "fake-embed", Monday, 0.5);

    [Fact]
    public void A_link_is_removed_once_and_kept_as_history()
    {
        // Arrange
        var link = EvidenceStoryLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Anna, Monday, LinkSource.AiApproved);

        // Act
        link.Remove(Anna, Monday);

        // Assert
        Assert.False(link.IsActive);
        Assert.Equal(LinkSource.AiApproved, link.Source);
        Assert.Throws<DomainException>(() => link.Remove(Anna, Monday));
    }

    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(-1, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(0.123456, 0.1235)]
    public void The_score_is_kept_between_0_and_1(double score, double expected)
    {
        // Act / Assert
        Assert.Equal(expected, Suggestion(score).Score);
    }

    [Fact]
    public void The_rank_starts_at_one()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Suggestion(rank: 0));
    }

    [Fact]
    public void A_suggestion_is_decided_once()
    {
        // Arrange
        var suggestion = Suggestion();

        // Act
        suggestion.Approve(Anna, Monday);

        // Assert
        Assert.Equal(SuggestionStatus.Approved, suggestion.Status);
        Assert.Throws<DomainException>(() => suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday));
    }

    [Fact]
    public void Rejecting_needs_a_reason_and_other_needs_a_comment_cut_to_1000()
    {
        // Act / Assert
        Assert.Equal("Choose a reason", Assert.Throws<DomainException>(() => Suggestion().Reject(null, null, Anna, Monday)).Message);
        Assert.Equal("Explain the reason in a comment", Assert.Throws<DomainException>(() => Suggestion().Reject(RejectionReason.Other, " ", Anna, Monday)).Message);
        var suggestion = Suggestion();
        suggestion.Reject(RejectionReason.Other, new string('x', 1500), Anna, Monday);
        Assert.Equal(1000, suggestion.Comment!.Length);
    }

    [Fact]
    public void Replacing_only_touches_a_pending_suggestion()
    {
        // Arrange
        var pending = Suggestion();
        var approved = Suggestion();
        approved.Approve(Anna, Monday);

        // Act
        pending.Replace();
        approved.Replace();

        // Assert
        Assert.Equal((SuggestionStatus.Replaced, SuggestionStatus.Approved), (pending.Status, approved.Status));
    }
}
