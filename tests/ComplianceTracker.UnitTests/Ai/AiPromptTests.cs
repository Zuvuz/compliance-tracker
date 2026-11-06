using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Ai;

// Edge cases of what the AI is told and how its answers are read (US-060, US-061). Arrange / Act / Assert.
public class AiPromptTests
{
    private static AiProjectContext Context(string purpose = "A customer portal", string scope = "Web", string? classification = null,
        params (string, RiskLevel)[] risks)
        => new(purpose, scope, classification, classification is null ? null : new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), risks);

    // ---------------------------------------------------------- Reading the "not applicable" answer

    [Theory]
    [InlineData("NOT APPLICABLE: The product has no network interface", "The product has no network interface")]
    [InlineData("not applicable: lower case is fine", "lower case is fine")]
    [InlineData("   NOT APPLICABLE:   spaces around  ", "spaces around")]
    [InlineData("NOT APPLICABLE: first line\nsecond line is ignored", "first line")]
    public void A_clear_not_applicable_answer_gives_its_reason(string answer, string reason)
    {
        // Arrange (nothing)

        // Act
        var read = ApplicabilityPrompt.ReasonFrom(answer);

        // Assert
        Assert.Equal(reason, read);
    }

    [Theory]
    [InlineData("APPLIES")]
    [InlineData("")]
    [InlineData("NOT APPLICABLE:")]
    [InlineData("I think it is NOT APPLICABLE: because")]
    [InlineData("Not applicable, the product is offline")]
    [InlineData("APPLIES\nNOT APPLICABLE: on the second line")]
    public void Anything_but_a_clear_answer_on_the_first_line_means_it_applies(string answer)
    {
        // Arrange (nothing)

        // Act
        var read = ApplicabilityPrompt.ReasonFrom(answer);

        // Assert
        Assert.Null(read);
    }

    // ---------------------------------------------------------- The context

    [Fact]
    public void Empty_purpose_and_scope_are_said_to_be_missing_so_the_AI_does_not_guess()
    {
        // Arrange
        var context = Context(purpose: " ", scope: "");

        // Act
        var text = context.PromptText();

        // Assert
        Assert.Contains("Intended purpose: (not given)", text);
        Assert.Contains("Scope: (not given)", text);
        Assert.Contains("Classification under this regulation: (not classified)", text);
        Assert.Contains("Open risks tied to this requirement: (none)", text);
        Assert.Equal("", context.Used);
    }

    [Fact]
    public void The_context_used_names_every_part_in_order()
    {
        // Arrange
        var context = Context(classification: "Important, class I", risks: [("Stolen sessions", RiskLevel.High), ("Phishing", RiskLevel.Low)]);

        // Act
        var used = context.Used;

        // Assert
        Assert.Equal("purpose, scope, classification, risk 'Stolen sessions', risk 'Phishing'", used);
    }

    [Theory]
    [InlineData(RiskLevel.Low, false)]
    [InlineData(RiskLevel.Medium, false)]
    [InlineData(RiskLevel.High, true)]
    [InlineData(RiskLevel.Critical, true)]
    public void Only_High_and_Critical_risks_make_a_requirement_high_risk(RiskLevel level, bool high)
    {
        // Arrange
        var context = Context(risks: [("A risk", level)]);

        // Act
        var result = context.HasHighRisk;

        // Assert
        Assert.Equal(high, result);
    }

    [Fact]
    public void The_requirement_code_comes_before_the_context_in_the_rationale_prompt()
    {
        // Arrange: the context could mention "Requirement", so the code must be found first
        var context = Context(purpose: "Requirement X: a decoy");

        // Act
        var prompt = RationalePrompt.For("Story", "Text", "I.2(d)", "Access control", context);

        // Assert
        Assert.True(prompt.IndexOf("Requirement I.2(d):", StringComparison.Ordinal) < prompt.IndexOf("Requirement X", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_context_the_rationale_prompt_has_no_product_section()
    {
        // Arrange (nothing)

        // Act
        var prompt = RationalePrompt.For("Story", "Text", "I.2(d)", "Access control");

        // Assert
        Assert.DoesNotContain("About the product", prompt);
    }
}
