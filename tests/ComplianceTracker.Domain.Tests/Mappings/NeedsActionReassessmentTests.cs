using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Tests.Mappings;

// US-156: edge cases of a new assessment asked for after a change, and of a dismissed mark. Arrange / Act / Assert.
public class NeedsActionReassessmentTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static NeedsActionReassessment Request(string reason = "intended purpose changed")
        => NeedsActionReassessment.Request(Guid.NewGuid(), Guid.NewGuid(), reason, Monday);

    [Fact]
    public void A_request_is_due_after_the_delay_and_not_before()
    {
        var request = Request();

        Assert.False(request.IsDue(Monday + NeedsActionReassessment.Delay - TimeSpan.FromSeconds(1)));
        Assert.True(request.IsDue(Monday + NeedsActionReassessment.Delay));
    }

    [Fact]
    public void Another_change_adds_its_reason_and_waits_again()
    {
        var request = Request();

        request.Again("scope changed", Monday.AddMinutes(1));

        Assert.Equal("intended purpose changed; scope changed", request.Reasons);
        Assert.False(request.IsDue(Monday + NeedsActionReassessment.Delay));
        Assert.True(request.IsDue(Monday.AddMinutes(1) + NeedsActionReassessment.Delay));
    }

    [Fact]
    public void The_same_reason_twice_is_said_once()
    {
        var request = Request();

        request.Again("intended purpose changed", Monday.AddMinutes(1));
        request.Again("  ", Monday.AddMinutes(2));

        Assert.Equal("intended purpose changed", request.Reasons);
    }

    [Fact]
    public void Many_reasons_are_cut_to_the_limit()
    {
        var request = Request();

        for (var i = 0; i < 200; i++)
        {
            request.Again($"change number {i}", Monday.AddMinutes(1));
        }

        Assert.Equal(NeedsActionReassessment.MaxReasonsLength, request.Reasons.Length);
    }

    [Fact]
    public void A_finished_request_is_not_due_and_cannot_be_asked_again()
    {
        var request = Request();
        request.WaitForAi();

        request.Finish("2 need action, 0 do not apply", Monday.AddMinutes(10));

        Assert.Equal((true, false, false), (request.IsDone, request.WaitingForAi, request.IsDue(Monday.AddDays(1))));
        Assert.Throws<InvalidOperationException>(() => request.Again("scope changed", Monday.AddDays(1)));
    }

    // ---------------------------------------------------------- R3: a person's decision is kept

    private static NeedsActionSuggestion Mark()
        => NeedsActionSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "remote sign-in", NeedsActionPriority.High, false, "", "m", "v", Monday);

    [Fact]
    public void Only_a_dismissed_mark_is_marked_as_based_on_an_earlier_description()
    {
        var pending = Mark();
        var confirmed = Mark();
        confirmed.Confirm(Guid.NewGuid(), Monday, null);
        var dismissed = Mark();
        dismissed.Dismiss("No remote management", Guid.NewGuid(), Monday, null);

        foreach (var mark in new[] { pending, confirmed, dismissed })
        {
            mark.MarkContextChanged(Monday.AddDays(1));
        }

        Assert.Equal((null, null, Monday.AddDays(1)), (pending.ContextChangedAt, confirmed.ContextChangedAt, dismissed.ContextChangedAt));
        Assert.Equal(NeedsActionStatus.Dismissed, dismissed.Status);
    }
}
