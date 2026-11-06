using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of ApplicabilitySuggestion (US-060, US-061). Every test follows Arrange / Act / Assert.
public class ApplicabilitySuggestionTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicabilitySuggestion Create(string reason = "The product has no network interface", DateTime? classifiedAt = null)
        => ApplicabilitySuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, "purpose, scope", classifiedAt,
            "qwen2.5:3b", "applicability-v1", Monday);

    [Fact]
    public void A_new_suggestion_is_pending_and_keeps_its_context_model_and_prompt()
    {
        // Arrange
        var classifiedAt = Monday.AddDays(-1);

        // Act
        var suggestion = Create(classifiedAt: classifiedAt);

        // Assert
        Assert.True(suggestion.IsPending);
        Assert.Equal("purpose, scope", suggestion.ContextUsed);
        Assert.Equal("qwen2.5:3b", suggestion.Model);
        Assert.Equal("applicability-v1", suggestion.PromptVersion);
        Assert.Equal(classifiedAt, suggestion.ClassifiedAt);
    }

    [Fact]
    public void A_suggestion_needs_a_reason()
    {
        // Arrange (nothing)

        // Act
        var error = Assert.Throws<DomainException>(() => Create(reason: " "));

        // Assert
        Assert.Equal("The AI gave no reason", error.Message);
    }

    [Fact]
    public void Accepting_keeps_who_when_and_the_role()
    {
        // Arrange
        var suggestion = Create();
        var anna = Guid.NewGuid();

        // Act
        suggestion.Accept(anna, Monday, UserRole.ComplianceManager);

        // Assert
        Assert.Equal(SuggestionStatus.Approved, suggestion.Status);
        Assert.Equal(anna, suggestion.DecidedById);
        Assert.Equal(UserRole.ComplianceManager, suggestion.DecidedRole);
    }

    [Fact]
    public void A_decided_suggestion_cannot_be_decided_again()
    {
        // Arrange
        var suggestion = Create();
        suggestion.Reject(Guid.NewGuid(), Monday, null);

        // Act
        var error = Assert.Throws<DomainException>(() => suggestion.Accept(Guid.NewGuid(), Monday, null));

        // Assert
        Assert.Equal("The suggestion has already been decided", error.Message);
    }

    [Fact]
    public void A_new_classification_replaces_only_pending_suggestions()
    {
        // Arrange
        var pending = Create();
        var accepted = Create();
        accepted.Accept(Guid.NewGuid(), Monday, null);

        // Act
        pending.Replace();
        accepted.Replace();

        // Assert
        Assert.Equal(SuggestionStatus.Replaced, pending.Status);
        Assert.Equal(SuggestionStatus.Approved, accepted.Status);
    }
}
