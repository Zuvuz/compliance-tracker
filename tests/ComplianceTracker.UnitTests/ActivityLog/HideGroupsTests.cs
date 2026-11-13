using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.ActivityLog;

// Edge-case unit tests of #52: hiding groups of the activity log. Arrange / Act / Assert.
public class HideGroupsTests
{
    [Fact]
    public void Without_a_group_or_hidden_groups_every_action_is_shown()
        => Assert.Null(new ActivityLogQuery().Actions());

    [Fact]
    public void Hiding_sign_ins_leaves_every_other_action()
    {
        var actions = new ActivityLogQuery(Hidden: [ActivityGroup.SignIn]).Actions()!;

        Assert.DoesNotContain(ActivityAction.UserSignedIn, actions);
        Assert.Contains(ActivityAction.EvidenceAdded, actions);
        Assert.Equal(Enum.GetValues<ActivityAction>().Length - ActivityGroups.ActionsIn(ActivityGroup.SignIn).Count, actions.Count);
    }

    [Fact]
    public void A_chosen_group_that_is_also_hidden_shows_nothing()
        => Assert.Empty(new ActivityLogQuery(Group: ActivityGroup.Evidence, Hidden: [ActivityGroup.Evidence]).Actions()!);

    [Fact]
    public void Hidden_groups_count_as_a_filter()
    {
        Assert.True(new ActivityLogQuery(Hidden: [ActivityGroup.SignIn]).HasFilters);
        Assert.False(new ActivityLogQuery(Hidden: []).HasFilters);
    }

    [Fact]
    public void Hide_is_read_from_repeated_and_comma_separated_values_once_each()
    {
        var (query, ignored) = ActivityFilters.Parse(null, null, null, null, null, null, null, ["signin, Members", "signin", ""]);

        Assert.Equal([ActivityGroup.SignIn, ActivityGroup.Members], query.Hidden);
        Assert.Empty(ignored);
    }

    [Fact]
    public void An_unknown_group_is_ignored_and_said_so()
    {
        var (query, ignored) = ActivityFilters.Parse(null, null, null, null, null, null, null, ["nonsense"]);

        Assert.Null(query.Hidden);
        Assert.Equal(["The action group 'nonsense' was not understood and is ignored"], ignored);
    }

    [Fact]
    public void The_next_page_keeps_the_hidden_groups()
    {
        var (query, _) = ActivityFilters.Parse(null, null, null, null, null, null, null, ["signin", "ai"]);

        Assert.Equal("signin,ai", ActivityFilters.RouteValues(query, 2)["hide"]);
    }
}
