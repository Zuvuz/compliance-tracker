using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Tests.Mappings;

// US-221: the AI's argument in five parts and the second confirmation. Every test follows Arrange / Act / Assert.
public class SuggestionArgumentTests
{
    private static readonly DateTime Monday = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private static SuggestionArgument Build(string? quote = null, PassageVerdict? verdict = null, string? missing = null, double score = 0.82)
        => SuggestionArgument.Build("I(2)(d)", "Protect against unauthorised access", score, quote, verdict, missing, null,
            ["nomic-embed-text", null, "qwen2.5:3b", "qwen2.5:3b"], "v4", Monday);

    // ---------------------------------------------------------- R3: the five parts

    [Fact]
    public void A_checked_suggestion_has_its_quote_and_the_confidence_from_the_score()
    {
        var argument = Build("the account is locked after 5 failed attempts", PassageVerdict.Partly, "Does not mention MFA.");

        Assert.Equal("I(2)(d): Protect against unauthorised access.", argument.Asks);
        Assert.Equal("the account is locked after 5 failed attempts", argument.Quote);
        Assert.Equal("Does not mention MFA.", argument.Missing);
        Assert.Equal(SuggestionArgument.High, argument.Confidence);
        Assert.Equal("nomic-embed-text, qwen2.5:3b, prompt v4", argument.Models);
    }

    [Fact]
    public void Without_a_quote_it_says_so_and_is_never_more_than_low()
    {
        var argument = Build(score: 0.95);

        Assert.Null(argument.Quote);
        Assert.False(argument.HasQuote);
        Assert.Equal(SuggestionArgument.Low, argument.Confidence);
        Assert.Equal(SuggestionArgument.NotChecked, argument.Missing);
    }

    [Fact]
    public void A_passage_that_meets_it_has_nothing_missing()
        => Assert.Equal(SuggestionArgument.NothingMissing, Build("all sign-ins use MFA", PassageVerdict.Meets).Missing);

    [Theory]
    [InlineData(0.75, true, "High")]
    [InlineData(0.74, true, "Medium")]
    [InlineData(0.55, true, "Medium")]
    [InlineData(0.54, true, "Low")]
    [InlineData(0.99, false, "Low")]
    public void The_confidence_follows_the_score_and_the_quote(double score, bool hasQuote, string confidence)
        => Assert.Equal(confidence, SuggestionArgument.ConfidenceOf(score, hasQuote));

    [Theory]
    [InlineData("Protect against unauthorised access.", "I(2)(d): Protect against unauthorised access.")]
    [InlineData("  ", "I(2)(d)")]
    public void The_requirement_is_one_sentence_with_its_code(string title, string asks)
        => Assert.Equal(asks, SuggestionArgument.AsksOf("I(2)(d)", title));

    [Fact]
    public void A_blank_quote_counts_as_none()
        => Assert.Null(Build("   ").Quote);

    [Fact]
    public void Without_any_model_it_says_unknown()
        => Assert.Equal("Unknown", SuggestionArgument.Build("X", "Y", 0.5, null, null, null, null, [null, " "], null, Monday).Models);

    // ---------------------------------------------------------- R2, R6

    [Fact]
    public void An_approval_without_the_confirmation_is_refused()
    {
        var error = Assert.Throws<DomainException>(() => SuggestionArgument.RequireConfirmed(false));

        Assert.Equal(SuggestionArgument.ConfirmFirst, error.Message);
        SuggestionArgument.RequireConfirmed(true);
    }

    [Fact]
    public void The_log_says_the_argument_was_confirmed_with_the_confidence()
        => Assert.Equal("argument confirmed, confidence High", SuggestionArgument.Logged(SuggestionArgument.High));

    [Fact]
    public void An_approval_keeps_when_the_argument_was_confirmed()
    {
        var suggestion = MappingSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.86, 1, "nomic-embed-text", "v1", Monday);

        suggestion.Approve(Guid.NewGuid(), Monday, null, Monday.AddMinutes(1));

        Assert.Equal(Monday.AddMinutes(1), suggestion.ArgumentConfirmedAt);
    }
}
