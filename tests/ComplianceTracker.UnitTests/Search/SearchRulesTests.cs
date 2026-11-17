using ComplianceTracker.Application.Search;

namespace ComplianceTracker.UnitTests.Search;

// US-100: the rules of the search term and the results. Edge cases. Arrange / Act / Assert.
public class SearchRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Nothing_to_search_for_is_null(string? term)
        => Assert.Null(SearchRules.Clean(term));

    [Fact]
    public void A_term_is_trimmed_and_its_spaces_made_one()
        => Assert.Equal("MFA sign in", SearchRules.Clean("  MFA   sign\tin "));

    [Fact]
    public void A_long_term_is_cut_to_200_characters()
    {
        var clean = SearchRules.Clean(new string('a', 500));

        Assert.Equal(SearchRules.MaxTermLength, clean!.Length);
    }

    [Fact]
    public void Characters_that_mean_something_to_like_are_searched_for_as_they_are()
    {
        Assert.Equal(@"%100\%%", SearchRules.Like("100%"));
        Assert.Equal(@"%a\_b%", SearchRules.Like("a_b"));
        Assert.Equal(@"%c:\\x%", SearchRules.Like(@"c:\x"));
    }

    [Fact]
    public void Quotes_and_sql_are_left_as_text()
        => Assert.Equal("%' OR 1=1 --%", SearchRules.Like("' OR 1=1 --"));

    [Fact]
    public void An_empty_result_is_empty_and_one_hit_is_not()
    {
        var empty = new SearchResult("MFA", [new SearchGroup(SearchKind.Risk, [], false)]);
        var one = new SearchResult("MFA", [new SearchGroup(SearchKind.Risk, [new SearchHit(SearchKind.Risk, Guid.NewGuid(), "Phishing", null, Guid.NewGuid(), "")], false)]);

        Assert.True(empty.IsEmpty);
        Assert.True(new SearchResult("", []).IsEmpty);
        Assert.False(one.IsEmpty);
    }

    [Fact]
    public void Every_kind_has_a_name()
        => Assert.All(Enum.GetValues<SearchKind>(), k => Assert.False(string.IsNullOrWhiteSpace(SearchRules.KindName(k))));
}
