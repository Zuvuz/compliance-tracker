using ComplianceTracker.Domain.Ai;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Tests.Ai;

// Unit tests of AiEvaluation (US-116). Every test follows Arrange / Act / Assert.
public class AiEvaluationTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_matching_run_keeps_its_shares_in_per_cent()
    {
        // Arrange (nothing)

        // Act
        var run = AiEvaluation.ForMatching("nomic-embed-text", "mapping-v3", 240, Monday.AddDays(-30), Monday,
            0.5, 0.71, 0.9, 0.14, "Ops", Monday);

        // Assert
        Assert.Equal(AiEvaluationStep.StoryMatching, run.Step);
        Assert.Equal(50, run.Top1);
        Assert.Equal(71, run.Top3);
        Assert.Equal(90, run.Top10);
        Assert.Equal(14, run.RejectedAboveLimit);
        Assert.Equal(240, run.Decisions);
    }

    [Fact]
    public void A_rationale_run_keeps_the_average_grade()
    {
        // Arrange
        int[] grades = [4, 5, 3];

        // Act
        var run = AiEvaluation.ForRationales("qwen2.5:3b", "mapping-v3", grades, "Ops", Monday);

        // Assert
        Assert.Equal(AiEvaluationStep.Rationale, run.Step);
        Assert.Equal(4, run.AverageGrade);
        Assert.Equal(3, run.Graded);
    }

    [Theory]
    [InlineData(new int[0], "Grade at least one rationale")]
    [InlineData(new[] { 0 }, "A grade must be from 1 to 5")]
    [InlineData(new[] { 6 }, "A grade must be from 1 to 5")]
    public void Grades_must_be_from_1_to_5(int[] grades, string message)
    {
        // Arrange (nothing)

        // Act
        var error = Assert.Throws<DomainException>(() => AiEvaluation.ForRationales("m", "p", grades, "Ops", Monday));

        // Assert
        Assert.Equal(message, error.Message);
    }
}
