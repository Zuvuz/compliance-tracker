using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-215: the year wheel of the yearly plan. Every test follows Arrange / Act / Assert.
public class YearWheelTests
{
    private static readonly DateTime Monday = new(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);

    private static RecurringTask Task(string title, TaskInterval interval, DateOnly due)
        => RecurringTask.Create(Guid.NewGuid(), title, null, interval, Guid.NewGuid(), due, [], null, null, Monday);

    [Fact]
    public void The_wheel_has_twelve_months_from_this_month()
    {
        var months = YearlyPlanService.Months([], new DateOnly(2026, 11, 25));

        Assert.Equal(12, months.Count);
        Assert.Equal(new DateOnly(2026, 11, 1), months[0].Month);
        Assert.Equal(new DateOnly(2027, 10, 1), months[^1].Month);
    }

    [Fact]
    public void A_quarterly_task_shows_four_times()
    {
        var months = YearlyPlanService.Months([Task("Access review", TaskInterval.Quarterly, new DateOnly(2026, 12, 31))], new DateOnly(2026, 11, 25));

        Assert.Equal(4, months.Sum(m => m.Deadlines.Count));
        Assert.Single(months.Single(m => m.Month == new DateOnly(2027, 3, 1)).Deadlines);
    }

    [Fact]
    public void An_overdue_deadline_shows_in_this_month()
    {
        var months = YearlyPlanService.Months([Task("Restore test", TaskInterval.Yearly, new DateOnly(2026, 9, 30))], new DateOnly(2026, 11, 25));

        Assert.Equal(new DateOnly(2026, 9, 30), months[0].Deadlines.Single().DueOn);
    }
}
