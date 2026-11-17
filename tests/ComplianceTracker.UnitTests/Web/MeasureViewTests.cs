using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// #52: edge cases of what a measure on a risk's page says stands behind it. Arrange / Act / Assert.
public class MeasureViewTests
{
    [Theory]
    [InlineData(0, 0, "0 stories · 0 evidence")]
    [InlineData(1, 0, "1 story · 0 evidence")]
    [InlineData(2, 3, "2 stories · 3 evidence")]
    public void The_counts_say_story_or_stories(int stories, int evidence, string expected)
        => Assert.Equal(expected, MeasureView.Counts(stories, evidence));

    [Fact]
    public void A_measure_without_compliance_data_leads_nowhere_and_says_nothing()
    {
        var view = new MeasureView(null, null, [], "");

        Assert.Null(view.Code);
        Assert.Empty(view.StoryRequirements);
        Assert.False(view.NeedsEvidence);
    }
}
