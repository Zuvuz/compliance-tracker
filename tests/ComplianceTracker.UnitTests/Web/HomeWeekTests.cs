using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Isms;
using ComplianceTracker.Application.Review;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-227: "Your week" on Home: the tasks, the ISMS items and the approvals as one list, overdue first, up to 7 days ahead.
// Arrange / Act / Assert.
public class HomeWeekTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly Guid ProjectA = Guid.NewGuid();
    private static readonly Guid ProjectB = Guid.NewGuid();

    private static TaskItem Task(string title, int days, Guid? project = null, TaskKind kind = TaskKind.RiskReview, bool canAct = true)
        => new(project ?? ProjectA, "Portal", kind, Guid.NewGuid(), title, Now.AddDays(days), $"/risk/{title}", canAct);

    private static WeekItem Isms(string text, int days) => new(text, Today.AddDays(days), "YearlyPlan");

    private static ApprovalView Approval(string summary, Guid? project, bool canApprove, DateTime? requestedAt = null)
    {
        var request = ApprovalRequest.Create(Guid.NewGuid(), project, ApprovalAction.AcceptRisk, Guid.NewGuid(), null, summary, "{}",
            Guid.NewGuid(), requestedAt ?? Now.AddDays(-1));
        return new ApprovalView(request, "Bo", null, canApprove, !canApprove, false);
    }

    private static (IReadOnlyList<HomeItem> Week, IReadOnlyList<HomeItem> Later) Build(
        IEnumerable<TaskItem>? tasks = null, IEnumerable<WeekItem>? isms = null, IEnumerable<ApprovalView>? approvals = null,
        Guid? project = null, TaskKind? kind = null)
        => HomeWeek.Build(tasks ?? [], isms ?? [], approvals ?? [], Now, i => $"/isms/{i.Page}", a => $"/approve/{a.Request.Id}", project, kind);

    [Fact]
    public void A_member_with_nothing_to_do_has_an_empty_week()
    {
        var (week, later) = Build();

        Assert.Empty(week);
        Assert.Empty(later);
    }

    [Fact]
    public void Everything_is_one_list_overdue_first()
    {
        var (week, _) = Build([Task("Review the risk", 3)], [Isms("Review access rights", -6)], [Approval("Accept Data leak", ProjectA, true)]);

        Assert.Equal(["Review access rights", "Review the risk", "Approve: Accept Data leak"], week.Select(i => i.Title).Take(2).Append(week[2].Title));
        Assert.Equal(HomeWeek.Isms, week[0].Kind);
    }

    [Fact]
    public void The_approval_is_due_when_it_expires()
    {
        var (week, _) = Build(approvals: [Approval("Accept Data leak", ProjectA, true, Now.AddDays(-2))]);

        Assert.Equal(Now.AddDays(-2) + ApprovalRequest.ExpiresAfter, Assert.Single(week).Due);
    }

    [Fact]
    public void An_isms_item_due_today_is_not_overdue_until_the_day_is_over()
    {
        var (week, _) = Build(isms: [Isms("Measure the objective", 0)]);

        Assert.False(MyTaskPlan.IsOverdue(Assert.Single(week).Due, Now));
    }

    [Fact]
    public void Beyond_seven_days_is_later()
    {
        var (week, later) = Build([Task("Soon", 7), Task("Far", 8)], [Isms("Next month", 30)]);

        Assert.Equal(["Soon"], week.Select(i => i.Title));
        Assert.Equal(["Far", "Next month"], later.Select(i => i.Title));
    }

    [Fact]
    public void My_own_request_waits_for_someone_else_and_is_not_listed()
    {
        var (week, later) = Build(approvals: [Approval("Accept Data leak", ProjectA, canApprove: false)]);

        Assert.Empty(week.Concat(later));
    }

    [Fact]
    public void A_task_my_role_can_only_read_has_no_link()
    {
        var (week, _) = Build([Task("Evidence expires", 1, canAct: false)]);

        Assert.Null(Assert.Single(week).Href);
    }

    [Fact]
    public void A_chosen_project_leaves_out_the_isms_and_other_projects()
    {
        var (week, _) = Build([Task("A", 1, ProjectA), Task("B", 1, ProjectB)], [Isms("ISMS", 1)],
            [Approval("In A", ProjectA, true), Approval("In B", ProjectB, true)], project: ProjectA);

        Assert.Equal(["A", "Approve: In A"], week.Select(i => i.Title));
    }

    [Fact]
    public void A_chosen_kind_shows_only_tasks_of_that_kind()
    {
        var (week, _) = Build([Task("Review", 1), Task("Evidence", 1, kind: TaskKind.EvidenceExpires)], [Isms("ISMS", 1)],
            [Approval("Accept", ProjectA, true)], kind: TaskKind.EvidenceExpires);

        Assert.Equal(["Evidence"], week.Select(i => i.Title));
    }

    [Fact]
    public void Fifty_projects_are_sorted_together()
    {
        var tasks = Enumerable.Range(1, 50).Select(i => Task($"Task {i:00}", 7 - (i % 8), Guid.NewGuid())).ToList();

        var (week, later) = Build(tasks);

        Assert.Equal(50, week.Count + later.Count);
        Assert.Equal(week.OrderBy(i => i.Due).Select(i => i.Due), week.Select(i => i.Due));
    }

    [Fact]
    public void The_bell_counts_this_week()
        => Assert.Equal(2, HomeWeek.Count([Task("Soon", 2), Task("Far", 20)], [Isms("Overdue", -1)], [Approval("Mine", ProjectA, false)], Now));
}
