using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reminders;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Reminders;

// US-183: edge cases of the reminders. Arrange / Act / Assert.
public class ReminderTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static readonly DateTime Now = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    private static (Organization Organization, User Anna) Organisation()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna", "Anna", "anna@acme.example"));
        return (organization, organization.Members.Single());
    }

    // ---------------------------------------------------------- When (R1)

    [Theory]
    [InlineData(30, true)]   // exactly the days before
    [InlineData(31, false)]  // one day too early
    [InlineData(0, true)]    // the day itself
    [InlineData(-3, true)]   // passed: reminded once anyway
    public void A_deadline_is_due_within_the_days_before(int daysAhead, bool due)
        => Assert.Equal(due, ReminderPlan.IsDue(Today.AddDays(daysAhead), Today, 30));

    // ---------------------------------------------------------- Once per deadline (R2)

    [Fact]
    public void The_same_deadline_gives_one_reminder()
    {
        var anna = Guid.NewGuid();
        var due = new DueReminder(ReminderKind.EvidenceExpires, Subject, Now.AddDays(10), "Portal", "/", [anna]);
        var made = Reminder.Create(Acme, anna, ReminderKind.EvidenceExpires, Subject, Now.AddDays(10), Now);

        Assert.Empty(ReminderPlan.New([due], [made]));
    }

    [Fact]
    public void A_changed_deadline_gives_a_new_reminder()
    {
        var anna = Guid.NewGuid();
        var made = Reminder.Create(Acme, anna, ReminderKind.FindingDue, Subject, Now.AddDays(10), Now);

        Assert.Single(ReminderPlan.New([new DueReminder(ReminderKind.FindingDue, Subject, Now.AddDays(40), null, "/", [anna])], [made]));
    }

    [Fact]
    public void Two_people_get_one_reminder_each()
        => Assert.Equal(2, ReminderPlan.New([new DueReminder(ReminderKind.ApprovalWaiting, Subject, Now, null, "/", [Guid.NewGuid(), Guid.NewGuid()])], []).Count);

    // ---------------------------------------------------------- Retrying (R4)

    [Fact]
    public void A_failed_mail_is_tried_again_until_five_attempts()
    {
        var reminder = Reminder.Create(Acme, Guid.NewGuid(), ReminderKind.EvidenceExpires, Subject, Now, Now);
        for (var i = 0; i < Reminder.MaxAttempts - 1; i++)
        {
            reminder.Failed("down");
        }

        Assert.True(reminder.ShouldTry);
        reminder.Failed(new string('x', 2000));
        Assert.False(reminder.ShouldTry);
        Assert.Equal(Reminder.MaxErrorLength, reminder.LastError!.Length);
    }

    [Fact]
    public void A_sent_reminder_is_not_tried_again()
    {
        var reminder = Reminder.Create(Acme, Guid.NewGuid(), ReminderKind.EvidenceExpires, Subject, Now, Now);
        reminder.Failed("down");
        reminder.Sent(Now);

        Assert.False(reminder.ShouldTry);
        Assert.Null(reminder.LastError);
    }

    // ---------------------------------------------------------- What a person gets (R5)

    [Fact]
    public void A_kind_turned_off_is_not_sent_but_reporting_deadlines_always_are()
    {
        var (_, anna) = Organisation();
        anna.SetRemindersOff(Enum.GetValues<ReminderKind>());

        Assert.Empty(ReminderPlan.Wanting([anna], ReminderKind.EvidenceExpires));
        Assert.Equal([anna.Id], ReminderPlan.Wanting([anna], ReminderKind.ReportingDeadline));
        Assert.False(anna.HasTurnedOff(ReminderKind.ReportingDeadline));
    }

    [Fact]
    public void The_same_person_twice_gets_one_reminder()
    {
        var (_, anna) = Organisation();

        Assert.Single(ReminderPlan.Wanting([anna, anna], ReminderKind.ReportingDeadline));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void The_days_before_are_from_1_to_90(int days)
    {
        var (organization, _) = Organisation();

        Assert.Throws<DomainException>(() => organization.SetReminderDaysBefore(days));
        Assert.Equal(ReminderKinds.DefaultDaysBefore, organization.ReminderDaysBefore);
    }

    // ---------------------------------------------------------- The mail (R3)

    [Fact]
    public void The_mail_names_the_kind_the_deadline_and_a_link_but_no_title()
    {
        var due = new DueReminder(ReminderKind.EvidenceExpires, Subject, Now, "Acme Portal", "/Projects/Details/1/evidence", []);

        var body = ReminderPlan.Body("Acme ApS", due, "https://tracker.example/");

        Assert.Contains("evidence expires on 01-11-2026", body);
        Assert.Contains("https://tracker.example/Projects/Details/1/evidence", body);
        Assert.Contains("Project: Acme Portal", body);
    }

    [Fact]
    public void Without_a_base_address_the_mail_gives_the_path()
        => Assert.Contains("Open it in the tracker: /Approvals",
            ReminderPlan.Body("Acme ApS", new DueReminder(ReminderKind.ApprovalWaiting, Subject, Now, null, "/Approvals", []), null));
}
