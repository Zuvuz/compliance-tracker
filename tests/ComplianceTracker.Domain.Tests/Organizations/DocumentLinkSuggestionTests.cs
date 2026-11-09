using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Organizations;

// Edge-case unit tests of US-136 R2: the AI's suggestions on the organisation's documents. Arrange / Act / Assert.
public class DocumentLinkSuggestionTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Nis2 = Guid.NewGuid();

    private static DocumentLinkSuggestion Suggestion(string code = "Art. 21(2)(b)", double score = 0.82, int rank = 1)
        => DocumentLinkSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Nis2, code, score, rank, "nomic-embed-text", "document-v1", Monday, 0.5);

    [Fact]
    public void A_new_suggestion_is_pending_with_the_model_and_prompt_behind_it()
    {
        // Act
        var suggestion = Suggestion();

        // Assert
        Assert.True(suggestion.IsPending);
        Assert.Equal(("nomic-embed-text", "document-v1", 0.5), (suggestion.EmbeddingModel, suggestion.PromptVersion, suggestion.MinimumScore));
    }

    [Theory]
    [InlineData(1.7, 1)]
    [InlineData(-0.2, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(0.123456, 0.1235)]
    public void The_score_is_kept_between_0_and_1_with_four_decimals(double score, double expected)
    {
        // Act / Assert
        Assert.Equal(expected, Suggestion(score: score).Score);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A-very-long-code-that-is-longer-than-fifty-characters-x")]
    public void A_suggestion_needs_a_code_that_fits(string code)
    {
        // Act / Assert
        Assert.Equal("A suggestion needs the requirement's code", Assert.Throws<DomainException>(() => Suggestion(code)).Message);
    }

    [Fact]
    public void The_rank_starts_at_one()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Suggestion(rank: 0));
    }

    [Fact]
    public void The_code_is_matched_like_a_document_link_ignoring_case_and_spaces()
    {
        // Arrange
        var suggestion = Suggestion(" a.5.15 ");

        // Act / Assert
        Assert.Equal("a.5.15", suggestion.RequirementCode);
        Assert.True(suggestion.IsFor(Nis2, "A.5.15 "));
        Assert.False(suggestion.IsFor(Guid.NewGuid(), "A.5.15"));
    }

    [Fact]
    public void Approving_records_who_when_and_in_which_role()
    {
        // Arrange
        var suggestion = Suggestion();

        // Act
        suggestion.Approve(Anna, Monday, UserRole.ComplianceManager);

        // Assert
        Assert.Equal((SuggestionStatus.Approved, Anna, Monday, UserRole.ComplianceManager),
            (suggestion.Status, suggestion.DecidedById!.Value, suggestion.DecidedAt!.Value, suggestion.DecidedRole!.Value));
    }

    [Fact]
    public void A_suggestion_is_decided_only_once()
    {
        // Arrange
        var suggestion = Suggestion();
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday);

        // Act / Assert
        Assert.Equal("The suggestion has already been decided", Assert.Throws<DomainException>(() => suggestion.Approve(Anna, Monday)).Message);
        Assert.Throws<DomainException>(() => suggestion.Reject(RejectionReason.TooBroad, null, Anna, Monday));
    }

    [Fact]
    public void Rejecting_needs_a_reason()
    {
        // Act / Assert
        Assert.Equal("Choose a reason", Assert.Throws<DomainException>(() => Suggestion().Reject(null, null, Anna, Monday)).Message);
        Assert.Equal("Choose a reason", Assert.Throws<DomainException>(() => Suggestion().Reject((RejectionReason)99, null, Anna, Monday)).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Other_needs_a_comment(string? comment)
    {
        // Act / Assert
        Assert.Equal("Explain the reason in a comment",
            Assert.Throws<DomainException>(() => Suggestion().Reject(RejectionReason.Other, comment, Anna, Monday)).Message);
    }

    [Fact]
    public void A_long_comment_is_cut_to_1000_characters()
    {
        // Arrange
        var suggestion = Suggestion();

        // Act
        suggestion.Reject(RejectionReason.Other, new string('x', 1500), Anna, Monday);

        // Assert
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
        Assert.Equal(SuggestionStatus.Replaced, pending.Status);
        Assert.Equal(SuggestionStatus.Approved, approved.Status);
        Assert.Throws<DomainException>(() => pending.Approve(Anna, Monday));
    }
}
