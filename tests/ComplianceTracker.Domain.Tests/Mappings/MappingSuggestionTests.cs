using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of MappingSuggestion (US-020, US-036) and the organisation's AI sharing setting.
// Every test follows Arrange / Act / Assert.
public class MappingSuggestionTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static MappingSuggestion Create(double score = 0.86)
        => MappingSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), score, 1, "nomic-embed-text", "mapping-v1", Monday);

    [Fact]
    public void A_new_suggestion_is_pending_and_keeps_where_it_came_from()
    {
        // Arrange (nothing)

        // Act
        var suggestion = Create(score: 1.7);

        // Assert
        Assert.True(suggestion.IsPending);
        Assert.Equal(1, suggestion.Score);
        Assert.Equal("nomic-embed-text", suggestion.EmbeddingModel);
        Assert.Equal("mapping-v1", suggestion.PromptVersion);
        Assert.Null(suggestion.Rationale);
    }

    [Fact]
    public void The_rationale_is_written_once()
    {
        // Arrange
        var suggestion = Create();

        // Act
        suggestion.SetRationale(" Both are about sign-in. ", "qwen2.5:3b");
        suggestion.SetRationale("Something else", "other-model");

        // Assert
        Assert.Equal("Both are about sign-in.", suggestion.Rationale);
        Assert.Equal("qwen2.5:3b", suggestion.RationaleModel);
    }

    [Fact]
    public void A_rejection_needs_a_reason_and_Other_needs_a_comment()
    {
        // Arrange
        var suggestion = Create();

        // Act
        var noReason = Assert.Throws<DomainException>(() => suggestion.Reject(null, null, Guid.NewGuid(), Monday));
        var otherWithoutComment = Assert.Throws<DomainException>(() => suggestion.Reject(RejectionReason.Other, " ", Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("Choose a reason", noReason.Message);
        Assert.Equal("Explain the reason in a comment", otherWithoutComment.Message);
        Assert.True(suggestion.IsPending);
    }

    [Fact]
    public void A_decision_cannot_be_changed()
    {
        // Arrange
        var suggestion = Create();
        var anna = Guid.NewGuid();
        suggestion.Reject(RejectionReason.TooBroad, "Only sign-in", anna, Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => suggestion.Approve(Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("The suggestion has already been decided", error.Message);
        Assert.Equal(SuggestionStatus.Rejected, suggestion.Status);
        Assert.Equal(anna, suggestion.DecidedById);
    }

    [Fact]
    public void Replacing_only_affects_pending_suggestions()
    {
        // Arrange
        var pending = Create();
        var approved = Create();
        approved.Approve(Guid.NewGuid(), Monday);

        // Act
        pending.Replace();
        approved.Replace();

        // Assert
        Assert.Equal(SuggestionStatus.Replaced, pending.Status);
        Assert.Equal(SuggestionStatus.Approved, approved.Status);
    }

    [Fact]
    public void Only_an_Admin_can_switch_sharing_and_it_is_on_by_default()
    {
        // Arrange
        var organization = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var anna = organization.Users.Single();

        // Act
        var sharedAtStart = organization.ShareAiFeedback;
        var stranger = Assert.Throws<DomainException>(() => organization.SetShareAiFeedback(false, Guid.NewGuid()));
        var changed = organization.SetShareAiFeedback(false, anna.Id);

        // Assert
        Assert.True(sharedAtStart); // US-130 R1
        Assert.Equal("Only an Admin can change this setting", stranger.Message);
        Assert.True(changed);
        Assert.False(organization.ShareAiFeedback);
    }
}
