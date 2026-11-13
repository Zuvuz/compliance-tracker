using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.ActivityLog;

// US-152: edge-case unit tests of US-101 and US-102: the activity log's filters as they stand in the address.
// A value that cannot be read is ignored and said so (R9). Arrange / Act / Assert.
public class ActivityFilterTests
{
    private static (ActivityLogQuery Query, IReadOnlyList<string> Ignored) Parse(
        string? q = null, string? from = null, string? to = null, string? group = null, string? user = null, string? sort = null, string? page = null)
        => ActivityFilters.Parse(q, from, to, group, user, sort, page);

    [Fact]
    public void No_filters_give_the_newest_first_page_one_and_nothing_ignored()
    {
        var (query, ignored) = Parse();

        Assert.Equal((null, ActivitySort.Newest, 1, false), (query.Search, query.Sort, query.Page, query.HasFilters));
        Assert.Empty(ignored);
    }

    [Fact]
    public void The_search_text_is_trimmed_and_a_blank_one_is_no_search()
    {
        Assert.Equal("MFA", Parse(q: "  MFA ").Query.Search);
        Assert.Null(Parse(q: "   ").Query.Search);
    }

    [Theory]
    [InlineData("2026-11-01", null)]
    [InlineData("01-11-2026", "The date '01-11-2026' was not understood and is ignored")]
    [InlineData("2026-02-30", "The date '2026-02-30' was not understood and is ignored")]
    public void Dates_must_be_ISO_and_real(string date, string? message)
    {
        var (query, ignored) = Parse(from: date);

        Assert.Equal(message is null, query.From is not null);
        Assert.Equal(message is null ? [] : [message], ignored);
    }

    [Theory]
    [InlineData("EVIDENCE", ActivityGroup.Evidence)]
    [InlineData("signin", ActivityGroup.SignIn)]
    public void A_group_is_read_without_regard_to_case(string slug, ActivityGroup group)
        => Assert.Equal(group, Parse(group: slug).Query.Group);

    [Theory]
    [InlineData("system", true, false)]
    [InlineData("SYSTEM", true, false)]
    [InlineData("8f3c1d4e-0000-4000-8000-000000000001", false, true)]
    public void The_user_is_the_system_or_a_member_id(string user, bool system, bool member)
    {
        var query = Parse(user: user).Query;

        Assert.Equal((system, member), (query.SystemOnly, query.UserId is not null));
    }

    [Fact]
    public void A_user_that_is_neither_is_ignored_and_said_so()
        => Assert.Equal(["The user 'anna' was not understood and is ignored"], Parse(user: "anna").Ignored);

    [Theory]
    [InlineData("oldest", ActivitySort.Oldest)]
    [InlineData("Action", ActivitySort.Action)]
    [InlineData("random", ActivitySort.Newest)]
    public void An_unknown_sort_falls_back_to_newest(string sort, ActivitySort expected)
        => Assert.Equal(expected, Parse(sort: sort).Query.Sort);

    [Theory]
    [InlineData("3", 3, false)]
    [InlineData("0", 1, true)]
    [InlineData("-2", 1, true)]
    [InlineData("two", 1, true)]
    public void A_page_must_be_a_number_from_one(string page, int expected, bool ignored)
    {
        var (query, messages) = Parse(page: page);

        Assert.Equal((expected, ignored), (query.Page, messages.Count == 1));
    }

    [Fact]
    public void Several_bad_values_are_each_said_and_the_good_ones_still_used()
    {
        var (query, ignored) = Parse(q: "x", from: "bad", group: "nope", sort: "evidence");

        Assert.Equal("x", query.Search);
        Assert.Equal(3, ignored.Count);
    }

    [Fact]
    public void The_address_of_the_next_page_keeps_the_filters_and_leaves_out_the_defaults()
    {
        var (query, _) = Parse(q: "MFA", from: "2026-11-01", group: "evidence", user: "system", sort: "oldest");

        var values = ActivityFilters.RouteValues(query, page: 2);

        Assert.Equal(("MFA", "2026-11-01", "evidence", "system", "oldest", "2"),
            (values["q"], values["from"], values["group"], values["user"], values["sort"], values["page"]));
        Assert.Null(ActivityFilters.RouteValues(Parse().Query)["page"]);
        Assert.Null(ActivityFilters.RouteValues(Parse().Query)["sort"]);
    }

    [Fact]
    public void A_page_shows_where_it_is_and_whether_there_is_more()
    {
        var page = new ActivityLogPage([], Total: 120, Page: 3);

        Assert.Equal((101, 120, true, false), (page.First, page.Last, page.HasPrevious, page.HasNext));
        Assert.Equal((0, 0), (new ActivityLogPage([], 0, 1).First, new ActivityLogPage([], 0, 1).Last));
    }
}
