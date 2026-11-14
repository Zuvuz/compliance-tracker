using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Mappings;

// US-155: edge cases of the "needs action" mark. Arrange / Act / Assert.
public class NeedsActionSuggestionTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static NeedsActionSuggestion Mark(NeedsActionPriority priority = NeedsActionPriority.Medium, bool highRisk = false, string reason = "Remote sign-in")
        => NeedsActionSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, priority, highRisk, "purpose, scope", "model", "v1", Monday);

    [Fact]
    public void A_new_mark_is_pending_and_open()
    {
        var mark = Mark();

        Assert.Equal((NeedsActionStatus.Pending, true, true), (mark.Status, mark.IsPending, mark.IsOpen));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_mark_needs_a_reason(string reason)
        => Assert.Equal("The AI gave no reason", Assert.Throws<DomainException>(() => Mark(reason: reason)).Message);

    [Fact]
    public void A_long_reason_is_cut_and_trimmed()
    {
        var mark = Mark(reason: "  " + new string('x', NeedsActionSuggestion.MaxReasonLength + 50));

        Assert.Equal(NeedsActionSuggestion.MaxReasonLength, mark.Reason.Length);
    }

    [Fact]
    public void An_unknown_priority_is_refused()
        => Assert.Throws<DomainException>(() => Mark((NeedsActionPriority)7));

    [Theory]
    [InlineData(NeedsActionPriority.Low, true)]
    [InlineData(NeedsActionPriority.Medium, true)]
    [InlineData(NeedsActionPriority.High, false)]
    public void A_high_risk_makes_it_high_and_says_so_only_when_it_raised_it(NeedsActionPriority ai, bool raised)
    {
        var mark = Mark(ai, highRisk: true);

        Assert.Equal((NeedsActionPriority.High, raised), (mark.Priority, mark.RaisedByRisk));
    }

    [Fact]
    public void Without_a_risk_the_ais_priority_is_kept()
    {
        var mark = Mark(NeedsActionPriority.Low);

        Assert.Equal((NeedsActionPriority.Low, false), (mark.Priority, mark.RaisedByRisk));
    }

    [Fact]
    public void Confirming_keeps_the_mark_open_and_says_who()
    {
        var mark = Mark();

        mark.Confirm(Anna, Monday, UserRole.ComplianceManager);

        Assert.Equal((NeedsActionStatus.Confirmed, true, false, Anna, UserRole.ComplianceManager),
            (mark.Status, mark.IsOpen, mark.IsPending, mark.DecidedById, mark.DecidedRole));
    }

    [Fact]
    public void A_mark_can_be_confirmed_only_once()
    {
        var mark = Mark();
        mark.Confirm(Anna, Monday, null);

        Assert.Equal("The mark has already been decided", Assert.Throws<DomainException>(() => mark.Confirm(Anna, Monday, null)).Message);
    }

    [Fact]
    public void Dismissing_needs_a_reason_and_keeps_it()
    {
        var mark = Mark();

        Assert.Equal("Say why it is not needed", Assert.Throws<DomainException>(() => mark.Dismiss("  ", Anna, Monday, null)).Message);
        mark.Dismiss("  The IdP does it ", Anna, Monday, null);

        Assert.Equal((NeedsActionStatus.Dismissed, false, "The IdP does it"), (mark.Status, mark.IsOpen, mark.DecisionReason));
    }

    [Fact]
    public void A_confirmed_mark_can_still_be_dismissed_but_not_twice()
    {
        var mark = Mark();
        mark.Confirm(Anna, Monday, null);

        mark.Dismiss("Not needed after all", Anna, Monday, null);

        Assert.Equal(NeedsActionStatus.Dismissed, mark.Status);
        Assert.Throws<DomainException>(() => mark.Dismiss("Again", Anna, Monday, null));
    }

    [Fact]
    public void Replacing_only_touches_a_pending_mark()
    {
        var pending = Mark();
        var confirmed = Mark();
        confirmed.Confirm(Anna, Monday, null);

        pending.Replace();
        confirmed.Replace();

        Assert.Equal((NeedsActionStatus.Replaced, false), (pending.Status, pending.IsOpen));
        Assert.Equal(NeedsActionStatus.Confirmed, confirmed.Status);
    }
}
