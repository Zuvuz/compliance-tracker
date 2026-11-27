using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.ActivityLog;

// Edge-case unit tests of US-219: the time spent on a page, when a visit is over, who is on a page now, "Seen by", the
// retention period and turning the access log on. Arrange / Act / Assert.
public class AccessLogTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();
    private static readonly Guid Risk = Guid.NewGuid();
    private static readonly DateTime Ten = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private static PageVisit Visit(DateTime at, Guid? user = null, string? tab = "risks")
        => PageVisit.Start(Organisation, user ?? Bo, UserRole.ComplianceManager, Project, VisitKind.Risk, Risk, tab, "Risk: Data leak", at);

    // Signs of life every 30 seconds while visible, from the visit's last sign to the time given
    private static void VisibleUntil(PageVisit visit, DateTime until)
    {
        for (var at = visit.LastSeenAt.AddSeconds(30); at <= until; at = at.AddSeconds(30))
        {
            Assert.True(visit.SignOfLife(at, visible: true));
        }
    }

    // ---------------------------------------------------------- R2: the time spent

    // Signs of life every 30 seconds while hidden, as the page keeps sending them
    private static void HiddenUntil(PageVisit visit, DateTime until)
    {
        for (var at = visit.LastSeenAt.AddSeconds(30); at <= until; at = at.AddSeconds(30))
        {
            Assert.True(visit.SignOfLife(at, visible: false));
        }
    }

    [Fact]
    public void Bo_opens_a_risk_at_10_hides_the_tab_from_10_05_to_10_20_and_leaves_at_10_23_8_minutes_count()
    {
        var visit = Visit(Ten);
        VisibleUntil(visit, Ten.AddMinutes(5));
        visit.SignOfLife(Ten.AddMinutes(5), visible: false); // hidden at 10:05
        HiddenUntil(visit, Ten.AddMinutes(20));
        visit.SignOfLife(Ten.AddMinutes(20), visible: true); // shown again at 10:20
        VisibleUntil(visit, Ten.AddMinutes(23));
        Assert.True(visit.End(Ten.AddMinutes(23)));

        Assert.Equal(TimeSpan.FromMinutes(8), visit.TimeSpent);
    }

    [Fact]
    public void A_page_left_open_in_a_hidden_tab_keeps_its_visit_but_gets_no_time()
    {
        var visit = Visit(Ten);
        visit.SignOfLife(Ten.AddSeconds(30), visible: false);
        HiddenUntil(visit, Ten.AddHours(3));

        Assert.Equal(30, visit.SecondsSpent);
        Assert.True(visit.IsOnPage(Ten.AddHours(3)));
    }

    [Fact]
    public void Time_while_the_page_is_hidden_does_not_count()
    {
        var visit = Visit(Ten);
        VisibleUntil(visit, Ten.AddMinutes(5));
        visit.SignOfLife(Ten.AddMinutes(5), visible: false);
        visit.SignOfLife(Ten.AddMinutes(6), visible: true); // hidden 10:05–10:06, within the 2 minutes
        VisibleUntil(visit, Ten.AddMinutes(9));
        visit.End(Ten.AddMinutes(9));

        Assert.Equal(TimeSpan.FromMinutes(8), visit.TimeSpent); // 5 + 3, the hidden minute not
        Assert.Equal("8 minutes", AccessLogRules.Text(visit.TimeSpent));
    }

    [Fact]
    public void A_browser_that_closes_without_saying_goodbye_ends_2_minutes_after_its_last_sign_with_the_time_until_then()
    {
        var visit = Visit(Ten);
        VisibleUntil(visit, Ten.AddMinutes(4));

        Assert.True(visit.IsOnPage(Ten.AddMinutes(6)));
        Assert.False(visit.IsOnPage(Ten.AddMinutes(6).AddSeconds(1)));
        Assert.False(visit.SignOfLife(Ten.AddMinutes(30), visible: true)); // a laptop that wakes up later changes nothing
        Assert.Equal(TimeSpan.FromMinutes(4), visit.TimeSpent);
    }

    [Fact]
    public void After_goodbye_nothing_changes_and_the_visit_is_over_at_once()
    {
        var visit = Visit(Ten);
        visit.End(Ten.AddSeconds(20));

        Assert.True(visit.IsOver(Ten.AddSeconds(21)));
        Assert.False(visit.SignOfLife(Ten.AddSeconds(30), visible: true));
        Assert.False(visit.End(Ten.AddSeconds(40)));
        Assert.Equal(20, visit.SecondsSpent);
    }

    [Fact]
    public void A_sign_from_before_the_last_one_changes_nothing()
    {
        var visit = Visit(Ten);
        visit.SignOfLife(Ten.AddSeconds(30), visible: true);

        Assert.False(visit.SignOfLife(Ten.AddSeconds(10), visible: true));
        Assert.Equal(30, visit.SecondsSpent);
    }

    [Fact]
    public void A_visit_across_midnight_counts_the_whole_time()
    {
        var late = new DateTime(2026, 11, 20, 23, 58, 0, DateTimeKind.Utc);
        var visit = Visit(late);
        VisibleUntil(visit, late.AddMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), visit.TimeSpent);
        Assert.Equal(new DateOnly(2026, 11, 20), DateOnly.FromDateTime(visit.StartedAt));
        Assert.Equal(new DateOnly(2026, 11, 21), DateOnly.FromDateTime(visit.LastSeenAt));
    }

    [Fact]
    public void Two_tabs_on_the_same_page_count_the_time_once()
    {
        var first = Visit(Ten);
        var second = Visit(Ten.AddMinutes(1));
        VisibleUntil(first, Ten.AddMinutes(10));
        VisibleUntil(second, Ten.AddMinutes(6));

        Assert.Equal(TimeSpan.FromMinutes(15), first.TimeSpent + second.TimeSpent);
        Assert.Equal(TimeSpan.FromMinutes(10), AccessLogRules.TotalTime([first, second])); // 10:00–10:10 together
    }

    [Fact]
    public void Visits_far_apart_add_up_and_no_visits_is_no_time()
    {
        var morning = Visit(Ten);
        var afternoon = Visit(Ten.AddHours(4));
        VisibleUntil(morning, Ten.AddMinutes(3));
        VisibleUntil(afternoon, Ten.AddHours(4).AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(5), AccessLogRules.TotalTime([afternoon, morning]));
        Assert.Equal(TimeSpan.Zero, AccessLogRules.TotalTime([]));
    }

    [Theory]
    [InlineData(0, "less than a minute")]
    [InlineData(29, "less than a minute")]
    [InlineData(60, "1 minute")]
    [InlineData(480, "8 minutes")]
    [InlineData(3900, "1 h 5 min")]
    public void The_time_spent_is_written_in_words(int seconds, string expected)
        => Assert.Equal(expected, AccessLogRules.Text(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void A_visit_to_one_item_needs_the_item_and_a_tab_does_not()
    {
        Assert.Throws<DomainException>(() => PageVisit.Start(Organisation, Bo, UserRole.Admin, Project, VisitKind.Risk, null, null, "Risk", Ten));
        Assert.Equal("Tab", PageVisit.Start(Organisation, Bo, UserRole.Admin, Project, VisitKind.Tab, null, " risks ", " ", Ten).Title);
    }

    // ---------------------------------------------------------- R3: on the page now, and "Seen by"

    [Fact]
    public void On_the_page_now_shows_each_member_once_and_not_those_who_left()
    {
        var anna = Guid.NewGuid();
        var twoTabs = new[] { Visit(Ten), Visit(Ten.AddMinutes(1)) };
        var left = Visit(Ten, anna);
        left.End(Ten.AddMinutes(1));

        var now = AccessLogService.OnPage([.. twoTabs, left], Ten.AddMinutes(2));

        Assert.Equal([Bo], now.Select(v => v.UserId));
        Assert.Same(twoTabs[1], now[0]); // the newest
    }

    [Fact]
    public void A_member_removed_while_on_a_page_is_named_a_former_member_and_drops_off_after_2_minutes()
    {
        var visit = Visit(Ten);
        VisibleUntil(visit, Ten.AddMinutes(1)); // removed at 10:01; the use case takes no more of their signs

        var seen = AccessLogService.Summary([visit], users: []);

        Assert.Equal("A former member", seen.Single().UserName);
        Assert.Equal(TimeSpan.FromMinutes(1), seen.Single().Total);
        Assert.Empty(AccessLogService.OnPage([visit], Ten.AddMinutes(4)));
    }

    // ---------------------------------------------------------- R4: who sees it

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.ComplianceManager, true)]
    [InlineData(UserRole.Auditor, true)]
    [InlineData(UserRole.Developer, false)]
    [InlineData(UserRole.Consultant, false)]
    public void Only_admins_compliance_managers_and_auditors_see_the_access_log(UserRole role, bool sees)
        => Assert.Equal(sees, AccessLogRules.CanSee(role));

    // ---------------------------------------------------------- R6, R7: the retention period and turning it on

    private static Organization NewOrganisation()
        => Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small, new UserIdentity("anna-sub", "Anna", "anna@acme.test"));

    private static readonly DateOnly Today = new(2026, 11, 27);

    [Fact]
    public void A_retention_period_of_0_or_more_than_5_years_is_refused_and_5_years_is_allowed()
    {
        var organisation = NewOrganisation();

        Assert.Throws<DomainException>(() => organisation.TurnOnAccessLog(Today.AddDays(42), 0, Today));
        Assert.Throws<DomainException>(() => organisation.TurnOnAccessLog(Today.AddDays(42), 61, Today));
        organisation.TurnOnAccessLog(Today.AddDays(42), 60, Today);
        Assert.Equal(60, organisation.AccessLogRetentionMonths);
    }

    [Fact]
    public void The_access_log_starts_at_least_6_weeks_ahead_and_is_off_until_then()
    {
        var organisation = NewOrganisation();

        Assert.False(organisation.IsAccessLogOn(Today));
        Assert.Throws<DomainException>(() => organisation.TurnOnAccessLog(Today.AddDays(41), 12, Today));
        organisation.TurnOnAccessLog(Today.AddDays(42), 12, Today);

        Assert.True(organisation.IsAccessLogAnnounced(Today));
        Assert.False(organisation.IsAccessLogOn(Today.AddDays(41)));
        Assert.True(organisation.IsAccessLogOn(Today.AddDays(42)));
    }

    [Fact]
    public void Once_on_the_start_date_stays_when_the_retention_period_changes_and_turning_off_is_at_once()
    {
        var organisation = NewOrganisation();
        organisation.TurnOnAccessLog(Today.AddDays(42), 12, Today);
        var later = Today.AddDays(100);

        organisation.TurnOnAccessLog(later, 24, later); // no new 6 weeks for a log that is already on
        Assert.Equal(Today.AddDays(42), organisation.AccessLogStartsOn);
        Assert.Equal(24, organisation.AccessLogRetentionMonths);

        organisation.TurnOffAccessLog();
        Assert.False(organisation.IsAccessLogOn(later));
        Assert.Null(organisation.AccessLogStartsOn);
    }

    [Fact]
    public void With_12_months_a_visit_from_1_november_2025_is_deleted_on_2_november_2026()
    {
        var before = AccessLogRules.DeleteBefore(new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc), 12);

        Assert.True(new DateTime(2025, 11, 1, 10, 0, 0, DateTimeKind.Utc) < before);
        Assert.False(new DateTime(2025, 11, 2, 10, 0, 0, DateTimeKind.Utc) < before);
        Assert.True(new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc) < AccessLogRules.DeleteBefore(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), 60));
    }

    [Fact]
    public void The_reports_have_one_row_per_visit_with_the_time_in_minutes()
    {
        var visit = Visit(Ten);
        VisibleUntil(visit, Ten.AddMinutes(8));

        var row = AccessLogService.CsvRows([new VisitRow(visit, "Bo", "Acme Portal")]).Single();

        Assert.Equal(AccessLogService.Columns.Length, row.Count);
        Assert.Equal(["2026-11-20T10:00:00Z", "2026-11-20T10:08:00Z", "8.0", "Bo", "Compliance manager", "Acme Portal", "Risk: Data leak"], row);
    }
}
