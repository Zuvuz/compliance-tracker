using ComplianceTracker.Application.Review;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Review;

// US-099: the rules of "My tasks": what counts as soon, the risks, the filter and the order, and the texts. Edge cases.
// Arrange / Act / Assert.
public class MyTaskPlanTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();

    [Theory]
    [InlineData(-30, true)] // overdue
    [InlineData(0, true)]
    [InlineData(7, true)] // exactly 7 days ahead still counts
    [InlineData(8, false)]
    public void Soon_is_overdue_or_within_7_days(int days, bool soon)
        => Assert.Equal(soon, MyTaskPlan.IsSoon(Now.AddDays(days), Now));

    [Fact]
    public void The_count_is_only_what_is_soon()
    {
        var tasks = new[] { Task(Now.AddDays(-1)), Task(Now.AddDays(3)), Task(Now.AddDays(30)) };

        Assert.Equal(2, MyTaskPlan.Count(tasks, Now));
        Assert.Equal(0, MyTaskPlan.Count([], Now));
    }

    [Fact]
    public void A_risk_whose_review_is_due_is_a_review_whoever_owns_it()
    {
        var risk = Risk(Bo, created: Now.AddMonths(-12).AddDays(-1));

        var (kind, _, due) = Assert.Single(MyTaskPlan.Risks([risk], Anna, Now));

        Assert.Equal(TaskKind.RiskReview, kind);
        Assert.True(MyTaskPlan.IsOverdue(due, Now));
    }

    [Fact]
    public void A_risk_I_own_that_is_not_due_is_listed_as_mine_and_another_persons_is_not()
    {
        var mine = Risk(Anna, created: Now.AddMonths(-1));
        var theirs = Risk(Bo, created: Now.AddMonths(-1));

        var (kind, risk, _) = Assert.Single(MyTaskPlan.Risks([mine, theirs], Anna, Now));

        Assert.Equal(TaskKind.RiskOwned, kind);
        Assert.Same(mine, risk);
    }

    [Fact]
    public void A_closed_risk_is_never_a_task()
    {
        var risk = Risk(Anna, created: Now.AddYears(-2));
        risk.Close("Gone", Now);

        Assert.Empty(MyTaskPlan.Risks([risk], Anna, Now));
    }

    [Fact]
    public void A_review_counts_from_the_last_review_not_from_creation()
    {
        var risk = Risk(Anna, created: Now.AddYears(-2));
        risk.MarkReviewed(Anna, Now.AddMonths(-1));

        Assert.Equal(Now.AddMonths(11), MyTaskPlan.ReviewDue(risk));
    }

    [Fact]
    public void The_filter_keeps_the_project_and_kind_and_sorts_by_due_date_then_title()
    {
        var portal = Guid.NewGuid();
        var tasks = new[]
        {
            Task(Now.AddDays(5), "b", portal),
            Task(Now.AddDays(5), "A", portal),
            Task(Now.AddDays(-2), "c", portal, TaskKind.RiskReview),
            Task(Now, "d")
        };

        Assert.Equal(["c", "A", "b"], MyTaskPlan.Filter(tasks, portal, null).Select(t => t.Title));
        Assert.Equal(["c"], MyTaskPlan.Filter(tasks, portal, TaskKind.RiskReview).Select(t => t.Title));
        Assert.Empty(MyTaskPlan.Filter(tasks, Guid.NewGuid(), null));
        Assert.Equal(4, MyTaskPlan.Filter(tasks, null, null).Count);
    }

    [Theory]
    [InlineData(-3, "Overdue by 3 days")]
    [InlineData(-1, "Overdue by 1 day")]
    [InlineData(0, "Due today")]
    [InlineData(1, "Due tomorrow")]
    [InlineData(7, "Due in 7 days")]
    [InlineData(8, "Due on 25-11-2026")]
    public void The_due_text_says_how_far_away(int days, string text)
        => Assert.Equal(text, MyTaskPlan.DueText(Now.AddDays(days), Now));

    [Fact]
    public void Earlier_the_same_day_is_due_today_not_overdue_in_words()
        => Assert.Equal("Due today", MyTaskPlan.DueText(Now.Date, Now));

    [Fact]
    public void Evidence_and_findings_need_linking_work_and_risks_need_risk_rights()
    {
        Assert.Equal(Permission.LinkWork, MyTaskPlan.RightFor(TaskKind.EvidenceExpires));
        Assert.Equal(Permission.LinkWork, MyTaskPlan.RightFor(TaskKind.FindingDue));
        Assert.Equal(Permission.ManageRisks, MyTaskPlan.RightFor(TaskKind.RiskReview));
        Assert.Equal(Permission.ManageRisks, MyTaskPlan.RightFor(TaskKind.ReportingDeadline));
    }

    [Fact]
    public void Every_kind_has_a_name_for_the_filter()
        => Assert.All(Enum.GetValues<TaskKind>(), k => Assert.False(string.IsNullOrWhiteSpace(MyTaskPlan.KindName(k))));

    private static TaskItem Task(DateTime due, string title = "t", Guid? project = null, TaskKind kind = TaskKind.EvidenceExpires)
        => new(project ?? Guid.NewGuid(), "Acme Portal", kind, Guid.NewGuid(), title, due, "/x", true);

    private static Risk Risk(Guid owner, DateTime created)
        => Domain.Risks.Risk.Create(Guid.NewGuid(), "Stolen session cookie", null, 3, 4, owner, RiskTreatment.Reduce, owner, created);
}
