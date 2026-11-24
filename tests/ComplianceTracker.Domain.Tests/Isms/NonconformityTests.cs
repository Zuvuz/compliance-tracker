using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-212: a nonconformity, its corrective action and the check that the action worked. Every test follows
// Arrange / Act / Assert.
public class NonconformityTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static Nonconformity Raised()
        => Nonconformity.Raise(Organisation, "Backups not tested", null, NonconformitySource.InternalAudit, null, NonconformitySeverity.Major, Anna, Monday);

    private static Nonconformity Done(bool withCause = true)
    {
        var nonconformity = Raised();
        if (withCause)
        {
            nonconformity.Analyse(null, "nobody owned the task");
        }

        nonconformity.PlanAction("Quarterly restore test", Anna, new DateOnly(2027, 1, 1), Today);
        nonconformity.MarkActionDone(Monday);
        return nonconformity;
    }

    // ---------------------------------------------------------- R1

    [Fact]
    public void A_nonconformity_has_a_title_a_source_and_a_severity_and_starts_open()
    {
        var nonconformity = Raised();

        Assert.Equal("Backups not tested", nonconformity.Title);
        Assert.Equal(NonconformitySource.InternalAudit, nonconformity.Source);
        Assert.Equal(NonconformitySeverity.Major, nonconformity.Severity);
        Assert.Equal(NonconformityStatus.Open, nonconformity.Status);
    }

    [Fact]
    public void A_nonconformity_needs_a_title_a_source_and_a_severity()
    {
        Assert.Throws<DomainException>(() => Nonconformity.Raise(Organisation, " ", null, NonconformitySource.Other, null, NonconformitySeverity.Minor, Anna, Monday));
        Assert.Throws<DomainException>(() => Nonconformity.Raise(Organisation, "X", null, null, null, NonconformitySeverity.Minor, Anna, Monday));
        Assert.Throws<DomainException>(() => Nonconformity.Raise(Organisation, "X", null, NonconformitySource.Other, null, (NonconformitySeverity)9, Anna, Monday));
    }

    [Fact]
    public void An_empty_source_id_is_no_source()
    {
        var nonconformity = Nonconformity.Raise(Organisation, "X", null, NonconformitySource.Complaint, Guid.Empty, NonconformitySeverity.Minor, Anna, Monday);

        Assert.Null(nonconformity.SourceId);
    }

    // ---------------------------------------------------------- R2: closing

    [Fact]
    public void Closing_without_a_cause_action_and_check_is_refused()
    {
        var nonconformity = Raised();

        var error = Assert.Throws<DomainException>(nonconformity.Close);

        Assert.Equal(Nonconformity.CloseFirst, error.Message);
    }

    [Fact]
    public void An_effective_check_without_a_cause_is_refused()
    {
        var nonconformity = Done(withCause: false);

        var error = Assert.Throws<DomainException>(() => nonconformity.Check("two restore tests done", true, Today, Anna, Monday));

        Assert.Equal(Nonconformity.CloseFirst, error.Message);
        Assert.Equal(NonconformityStatus.ActionDone, nonconformity.Status);
    }

    [Fact]
    public void An_effective_check_closes_it()
    {
        var nonconformity = Done();

        var closed = nonconformity.Check("two restore tests done", true, Today, Anna, Monday);

        Assert.True(closed);
        Assert.True(nonconformity.IsClosed);
        Assert.Equal(Monday, nonconformity.ClosedAt);
        nonconformity.Close();
    }

    // ---------------------------------------------------------- R3

    [Fact]
    public void The_check_before_the_action_is_done_is_refused()
    {
        var nonconformity = Raised();
        nonconformity.PlanAction("Quarterly restore test", Anna, new DateOnly(2027, 1, 1), Today);

        var error = Assert.Throws<DomainException>(() => nonconformity.Check("ok", true, Today, Anna, Monday));

        Assert.Equal("Mark the corrective action done before checking whether it worked", error.Message);
    }

    [Fact]
    public void An_action_cannot_be_done_before_it_is_planned_or_twice()
    {
        var nonconformity = Raised();

        Assert.Throws<DomainException>(() => nonconformity.MarkActionDone(Monday));
        nonconformity.PlanAction("Test", Anna, Today, Today);
        nonconformity.MarkActionDone(Monday);
        Assert.Throws<DomainException>(() => nonconformity.MarkActionDone(Monday));
    }

    // ---------------------------------------------------------- R4

    [Fact]
    public void A_check_that_found_it_did_not_work_opens_it_again_and_keeps_the_action()
    {
        var nonconformity = Done();

        var closed = nonconformity.Check("still no restore test", false, Today, Anna, Monday);

        Assert.False(closed);
        Assert.Equal(NonconformityStatus.Open, nonconformity.Status);
        Assert.Null(nonconformity.Action);
        var failed = Assert.Single(nonconformity.FailedActions);
        Assert.Equal("Quarterly restore test", failed.Action);
        Assert.Equal("still no restore test", failed.Result);
        Assert.Equal("nobody owned the task", nonconformity.Cause);
    }

    // ---------------------------------------------------------- R5 and the deadline

    [Fact]
    public void A_deadline_in_the_past_is_refused()
    {
        var nonconformity = Raised();

        var error = Assert.Throws<DomainException>(() => nonconformity.PlanAction("Test", Anna, Today.AddDays(-1), Today));

        Assert.Equal("The deadline has passed; choose a later date", error.Message);
    }

    [Fact]
    public void An_action_needs_a_text_an_owner_and_a_deadline()
    {
        var nonconformity = Raised();

        Assert.Throws<DomainException>(() => nonconformity.PlanAction(" ", Anna, Today, Today));
        Assert.Throws<DomainException>(() => nonconformity.PlanAction("Test", null, Today, Today));
        Assert.Throws<DomainException>(() => nonconformity.PlanAction("Test", Anna, null, Today));
    }

    [Fact]
    public void A_planned_action_past_its_deadline_is_overdue_until_it_is_done()
    {
        var nonconformity = Raised();
        nonconformity.PlanAction("Test", Anna, Today, Today);

        Assert.False(nonconformity.IsOverdue(Today));
        Assert.True(nonconformity.IsOverdue(Today.AddDays(1)));
        nonconformity.MarkActionDone(Monday);
        Assert.False(nonconformity.IsOverdue(Today.AddDays(1)));
    }

    // ---------------------------------------------------------- Reopening

    [Fact]
    public void A_closed_nonconformity_can_be_opened_again_with_a_reason()
    {
        var nonconformity = Done();
        nonconformity.Check("two restore tests done", true, Today, Anna, Monday);

        nonconformity.Reopen("the restore failed in March", Today.AddDays(90));

        Assert.Equal(NonconformityStatus.Open, nonconformity.Status);
        Assert.Null(nonconformity.ClosedAt);
        Assert.Null(nonconformity.CheckResult);
        Assert.Equal("Opened again: the restore failed in March", nonconformity.FailedActions.Single().Result);
    }

    [Fact]
    public void Only_a_closed_nonconformity_can_be_opened_again_and_it_needs_a_reason()
    {
        var open = Raised();
        Assert.Throws<DomainException>(() => open.Reopen("why", Today));

        var closed = Done();
        closed.Check("ok", true, Today, Anna, Monday);
        Assert.Throws<DomainException>(() => closed.Reopen("  ", Today));
    }

    [Fact]
    public void A_closed_nonconformity_cannot_be_changed()
    {
        var nonconformity = Done();
        nonconformity.Check("ok", true, Today, Anna, Monday);

        Assert.Throws<DomainException>(() => nonconformity.Analyse("x", "y"));
        Assert.Throws<DomainException>(() => nonconformity.PlanAction("Test", Anna, Today, Today));
    }

    [Fact]
    public void Analysing_names_what_changed()
    {
        var nonconformity = Raised();

        Assert.Equal(["correction", "cause"], nonconformity.Analyse("restored by hand", "nobody owned the task"));
        Assert.Empty(nonconformity.Analyse("restored by hand", "nobody owned the task"));
    }
}
