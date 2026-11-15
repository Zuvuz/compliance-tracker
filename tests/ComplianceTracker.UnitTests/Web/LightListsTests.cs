using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-154: edge cases of the light lists. Arrange / Act / Assert.
public class LightListsTests
{
    private static IReadOnlyList<int> Items(int count) => Enumerable.Range(1, count).ToList();

    [Theory]
    [InlineData(0, null, 0, false)]
    [InlineData(15, null, 15, false)]
    [InlineData(16, null, 15, true)]
    [InlineData(64, "all", 64, false)]
    [InlineData(64, "something-else", 15, true)]
    public void A_long_list_shows_the_first_items_until_all_is_asked_for(int count, string? sub, int shown, bool more)
    {
        var items = Items(count);

        Assert.Equal((shown, more), (LightLists.Visible(items, sub).Count, LightLists.HasMore(items, sub)));
    }

    [Fact]
    public void The_first_items_keep_their_order()
        => Assert.Equal(Enumerable.Range(1, 15), LightLists.Visible(Items(40), null));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("all", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", true)]
    public void Only_a_real_id_points_at_one_piece_of_evidence(string? sub, bool single)
        => Assert.Equal(single, LightLists.EvidenceOf(sub) is not null);
}
