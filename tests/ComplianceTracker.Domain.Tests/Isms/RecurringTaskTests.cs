using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-215: the yearly plan's recurring tasks and the starting plan. Every test follows Arrange / Act / Assert.
public class RecurringTaskTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2027, 1, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2027, 1, 5);

    private static RecurringTask AccessReview(string? evidence = null)
        => RecurringTask.Create(Organisation, "Access review", null, TaskInterval.Quarterly, Anna, new DateOnly(2026, 12, 31), ["A.5.18"], evidence, null, Monday);

    // ---------------------------------------------------------- R1

    [Fact]
    public void A_task_needs_a_title_an_interval_an_owner_and_a_deadline()
    {
        Assert.Throws<DomainException>(() => RecurringTask.Create(Organisation, " ", null, TaskInterval.Yearly, Anna, Today, [], null, null, Monday));
        Assert.Throws<DomainException>(() => RecurringTask.Create(Organisation, "X", null, null, Anna, Today, [], null, null, Monday));
        Assert.Throws<DomainException>(() => RecurringTask.Create(Organisation, "X", null, TaskInterval.Yearly, null, Today, [], null, null, Monday));
        Assert.Throws<DomainException>(() => RecurringTask.Create(Organisation, "X", null, TaskInterval.Yearly, Anna, null, [], null, null, Monday));
    }

    [Fact]
    public void Controls_and_clauses_are_cleaned_and_others_refused()
    {
        var task = RecurringTask.Create(Organisation, "X", null, TaskInterval.Yearly, Anna, Today, ["a.5.18", "9.3", "A.5.18"], null, null, Monday);

        Assert.Equal(["A.5.18", "9.3"], task.Controls);
        Assert.Throws<DomainException>(() => RecurringTask.Create(Organisation, "X", null, TaskInterval.Yearly, Anna, Today, ["Backup"], null, null, Monday));
    }

    // ---------------------------------------------------------- R2

    [Fact]
    public void A_done_task_gets_the_old_deadline_plus_the_interval()
    {
        var task = AccessReview();

        var completion = task.Complete(Today, Anna, null, null, Today, Monday);

        Assert.Equal(new DateOnly(2027, 3, 31), task.NextDueOn);
        Assert.Equal(new DateOnly(2026, 12, 31), completion.DueOn);
        Assert.True(completion.WasLate);
    }

    [Theory]
    [InlineData(TaskInterval.Monthly, "2027-01-31", "2027-02-28")]
    [InlineData(TaskInterval.HalfYearly, "2027-03-15", "2027-09-15")]
    [InlineData(TaskInterval.Yearly, "2028-02-29", "2029-02-28")]
    public void The_next_deadline_keeps_the_end_of_the_month(TaskInterval interval, string due, string next)
        => Assert.Equal(DateOnly.Parse(next), RecurringTask.Next(DateOnly.Parse(due), interval));

    [Fact]
    public void A_task_cannot_be_done_in_the_future()
        => Assert.Throws<DomainException>(() => AccessReview().Complete(Today.AddDays(1), Anna, null, null, Today, Monday));

    // ---------------------------------------------------------- R3

    [Fact]
    public void A_task_that_needs_evidence_cannot_be_done_without_it()
    {
        var task = AccessReview("access review list");

        var error = Assert.Throws<DomainException>(() => task.Complete(Today, Anna, null, null, Today, Monday));

        Assert.Equal("Access review needs evidence: access review list", error.Message);
        Assert.Equal(new DateOnly(2026, 12, 31), task.NextDueOn);
    }

    // ---------------------------------------------------------- R5

    [Fact]
    public void A_task_is_overdue_after_its_deadline()
    {
        var task = AccessReview();

        Assert.False(task.IsOverdue(new DateOnly(2026, 12, 31)));
        Assert.True(task.IsOverdue(Today));
    }

    [Fact]
    public void The_deadlines_of_the_next_months_follow_the_interval()
        => Assert.Equal([new DateOnly(2026, 12, 31), new DateOnly(2027, 3, 31), new DateOnly(2027, 6, 30)],
            AccessReview().DeadlinesUntil(new DateOnly(2027, 7, 31)));

    // ---------------------------------------------------------- R6

    [Fact]
    public void The_starting_plan_follows_the_controls_in_use()
    {
        var suggested = TaskSuggestion.For(["A.8.13"], []).Select(s => s.Title).ToList();

        Assert.Contains("Backup restore test", suggested);
        Assert.Contains("Management review", suggested);
        Assert.DoesNotContain("Access review", suggested);
    }

    [Fact]
    public void An_accepted_suggestion_is_not_suggested_again()
        => Assert.DoesNotContain(TaskSuggestion.For(["A.8.13"], ["restore-test"]), s => s.Key == "restore-test");

    [Fact]
    public void A_removed_task_cannot_be_done()
    {
        var task = AccessReview();
        task.Remove(Monday);

        Assert.False(task.IsOverdue(Today));
        Assert.Throws<DomainException>(() => task.Complete(Today, Anna, null, null, Today, Monday));
    }
}
