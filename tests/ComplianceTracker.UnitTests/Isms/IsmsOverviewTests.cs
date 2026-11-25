using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-216: my week, the status of each part of the ISMS and the guided start. Every test follows Arrange / Act / Assert.
public class IsmsOverviewTests
{
    private static readonly DateOnly Today = new(2026, 11, 21);
    private static readonly DateTime Monday = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    private static IsmsScope ApprovedScope()
    {
        var scope = IsmsScope.Create(Guid.NewGuid(), Monday);
        scope.Describe("Our SaaS products", null, null, null, null, null, null, Monday);
        scope.Include(Guid.NewGuid(), Monday);
        scope.Approve(Guid.NewGuid(), Monday);
        return scope;
    }

    private static IsmsFacts AllGood() => new(ApprovedScope(), 3, 0, 0, 2, 0, 0, 0, 0, 5, 0, 0, Today.AddMonths(-3), true, 0, 0, 0, true);

    private static IsmsPart Part(IsmsFacts facts, string name) => IsmsOverviewService.Parts(facts, Today).Single(p => p.Name == name);

    // ---------------------------------------------------------- R1, R2

    [Fact]
    public void My_week_has_the_overdue_first_and_keeps_the_rest_for_later()
    {
        var (week, later) = IsmsOverviewService.Week(
        [
            new WeekItem("Read the Access control policy", new DateOnly(2026, 11, 25), "Competence"),
            new WeekItem("Review access rights", new DateOnly(2026, 11, 15), "YearlyPlan"),
            new WeekItem("Hold the management review", new DateOnly(2026, 12, 10), "ManagementReview")
        ], Today);

        Assert.Equal(["Review access rights", "Read the Access control policy"], week.Select(i => i.Text));
        Assert.Equal("Hold the management review", later.Single().Text);
        Assert.True(week[0].IsOverdue(Today));
    }

    [Theory]
    [InlineData("Access review", "Review access rights")]
    [InlineData("MANAGEMENT review", "Hold the management review")]
    [InlineData("Clean the server room", "Do the task \"Clean the server room\"")]
    public void A_task_is_said_as_a_verb(string title, string verb) => Assert.Equal(verb, IsmsOverviewService.Verb(title));

    // ---------------------------------------------------------- R3, R4

    [Fact]
    public void Everything_in_place_is_green()
        => Assert.All(IsmsOverviewService.Parts(AllGood(), Today), p => Assert.Equal(PartState.Green, p.State));

    [Fact]
    public void No_management_review_for_14_months_is_red()
    {
        var part = Part(AllGood() with { LastReview = new DateOnly(2025, 9, 1) }, "Performance evaluation");

        Assert.Equal(PartState.Red, part.State);
        Assert.Equal("No management review for 14 months", part.Missing);
        Assert.Equal("ManagementReview", part.Page);
    }

    [Fact]
    public void A_review_due_next_month_is_yellow()
        => Assert.Equal(PartState.Yellow, Part(AllGood() with { LastReview = Today.AddMonths(-11) }, "Performance evaluation").State);

    [Fact]
    public void Red_wins_over_yellow_in_one_part()
    {
        var part = Part(AllGood() with { OverdueTasks = 2, TasksDueSoon = 1 }, "Operation");

        Assert.Equal(PartState.Red, part.State);
        Assert.Equal("2 tasks are overdue", part.Missing);
    }

    [Fact]
    public void No_scope_is_red_and_a_changed_scope_yellow()
    {
        Assert.Equal("The ISMS scope is not described", Part(AllGood() with { Scope = null }, "Context").Missing);

        var changed = ApprovedScope();
        changed.Describe("Our SaaS products and the app", null, null, null, null, null, null, Monday);
        Assert.Equal(PartState.Yellow, Part(AllGood() with { Scope = changed }, "Context").State);
    }

    [Fact]
    public void One_and_many_are_written_right()
    {
        Assert.Equal("1 policy is past the review date", Part(AllGood() with { PoliciesPastReview = 1 }, "Leadership").Missing);
        Assert.Equal("3 nonconformities are open", Part(AllGood() with { OpenNonconformities = 3 }, "Improvement").Missing);
    }

    [Theory]
    [InlineData("2025-09-01", "2026-11-21", 14)]
    [InlineData("2025-11-22", "2026-11-21", 11)]
    [InlineData("2025-11-21", "2026-11-21", 12)]
    public void Months_are_whole_months(string from, string to, int months)
        => Assert.Equal(months, IsmsOverviewService.Months(DateOnly.Parse(from), DateOnly.Parse(to)));

    // ---------------------------------------------------------- R5

    [Fact]
    public void A_new_organisation_with_a_scope_is_told_to_add_policies()
    {
        var overview = new IsmsOverview([], [], [], IsmsOverviewService.Guide(true, false, false, false, false, false, false), false, true, Today);

        Assert.Equal("Next: add your policies", overview.NextStep!.Next);
        Assert.True(overview.ShowsGuide);
    }

    [Fact]
    public void The_guide_is_gone_when_every_step_is_done_or_it_is_hidden()
    {
        Assert.False(new IsmsOverview([], [], [], IsmsOverviewService.Guide(true, true, true, true, true, true, true), false, true, Today).ShowsGuide);
        Assert.False(new IsmsOverview([], [], [], IsmsOverviewService.Guide(false, false, false, false, false, false, false), true, true, Today).ShowsGuide);
    }
}
