using System.Text;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.ActivityLog;

// Unit tests of the activity log export (US-055). Every test follows Arrange / Act / Assert.
public class ActivityLogExportTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    // ---------------------------------------------------------- The period (R2)

    [Fact]
    public void Without_dates_the_period_is_the_last_90_days()
        => Assert.Equal((new DateOnly(2026, 8, 14), Today), ActivityLogExportService.Period(new ActivityLogQuery(), Today));

    [Fact]
    public void Both_dates_are_kept()
        => Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            ActivityLogExportService.Period(new ActivityLogQuery(From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 31)), Today));

    [Fact]
    public void Only_a_start_reaches_90_days_after_it_but_not_past_today()
    {
        Assert.Equal(new DateOnly(2026, 4, 1), ActivityLogExportService.Period(new ActivityLogQuery(From: new DateOnly(2026, 1, 1)), Today).To);
        Assert.Equal(Today, ActivityLogExportService.Period(new ActivityLogQuery(From: new DateOnly(2026, 10, 1)), Today).To);
    }

    [Fact]
    public void Only_an_end_goes_back_90_days()
        => Assert.Equal(new DateOnly(2026, 3, 3), ActivityLogExportService.Period(new ActivityLogQuery(To: new DateOnly(2026, 6, 1)), Today).From);

    [Fact]
    public void Dates_the_wrong_way_round_are_swapped()
        => Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)),
            ActivityLogExportService.Period(new ActivityLogQuery(From: new DateOnly(2026, 2, 1), To: new DateOnly(2026, 1, 1)), Today));

    // ---------------------------------------------------------- The rows (R1, R3)

    private static (Organization Acme, User Anna) Acme()
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        return (acme, acme.Members.Single());
    }

    [Fact]
    public void A_row_has_the_time_in_UTC_the_project_the_person_and_their_role()
    {
        var (acme, anna) = Acme();
        var project = Guid.NewGuid();
        var entry = new ActivityLogEntry(acme.Id, anna.Id, ActivityAction.ProjectCreated, new DateTime(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc),
            project, "Acme Portal", UserRole.Admin);
        var subjects = new Dictionary<Guid, ActivitySubject> { [project] = new("Project", "Acme Portal", project) };

        var row = ActivityLogExportService.Rows([entry], subjects, acme.Users).Single();

        Assert.Equal(["2026-11-12T09:30:00Z", "ProjectCreated", "Acme Portal", "", "Anna", "Organisation admin", "Acme Portal"], row);
    }

    [Fact]
    public void What_the_tracker_did_itself_is_System_and_other_subjects_are_named()
    {
        var (acme, anna) = Acme();
        var entry = new ActivityLogEntry(acme.Id, null, ActivityAction.WorkItemsSynced, DateTime.UtcNow, anna.Id, null);
        var subjects = new Dictionary<Guid, ActivitySubject> { [anna.Id] = new("Member", "Anna", anna.Id) };

        var row = ActivityLogExportService.Rows([entry], subjects, acme.Users).Single();

        Assert.Equal(("", "Member: Anna", "System", "", ""), (row[2], row[3], row[4], row[5], row[6]));
    }

    [Fact]
    public void A_removed_or_unknown_person_and_subject_still_give_a_row()
    {
        var (acme, _) = Acme();
        var entry = new ActivityLogEntry(acme.Id, Guid.NewGuid(), ActivityAction.ProjectUpdated, DateTime.UtcNow, Guid.NewGuid(), "x");

        var row = ActivityLogExportService.Rows([entry], new Dictionary<Guid, ActivitySubject>(), acme.Users).Single();

        Assert.Equal(("", "", "Unknown"), (row[2], row[3], row[4]));
    }

    // ---------------------------------------------------------- CSV (R6)

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("plain", "plain")]
    public void No_cell_can_run_as_a_formula_or_break_the_row(string value, string expected)
        => Assert.Equal(expected, CsvFile.Cell(value));

    [Fact]
    public void The_file_starts_with_a_byte_order_mark_and_the_header()
    {
        var bytes = CsvFile.Write(["A", "B"], [["1", "2"]]);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("A;B\r\n1;2\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }
}
