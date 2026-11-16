using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Organizations;

// US-196: edge cases of an invitation by e-mail with an end date and a project, and of the member's access limits.
// Every test follows Arrange / Act / Assert.
public class InvitationByEmailTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Monday);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Invitation Invite(UserRole role, string? email = "carl@advisor.example", DateOnly? until = null, Guid? project = null)
        => Invitation.Create(Guid.NewGuid(), role, "hash", Anna, Monday, email, until, project);

    // ---------------------------------------------------------- The invitation (R1, R2)

    [Fact]
    public void The_e_mail_is_kept_trimmed_and_in_lower_case()
        => Assert.Equal("carl@advisor.example", Invite(UserRole.Consultant, "  Carl@Advisor.Example ").Email);

    [Fact]
    public void Without_an_e_mail_the_link_is_for_anyone_as_before()
        => Assert.Null(Invite(UserRole.Developer, email: "  ").Email);

    [Theory]
    [InlineData("carl")]
    [InlineData("carl@")]
    [InlineData("@advisor.example")]
    [InlineData("carl@@advisor.example")]
    [InlineData("carl @advisor.example")]
    [InlineData("carl@advisor")]
    public void A_wrong_e_mail_is_refused(string email)
        => Assert.Equal("Give a valid e-mail address", Assert.Throws<DomainException>(() => Invite(UserRole.Consultant, email)).Message);

    [Fact]
    public void A_too_long_e_mail_is_refused()
        => Assert.Throws<DomainException>(() => Invite(UserRole.Consultant, new string('a', 250) + "@a.dk"));

    [Fact]
    public void An_end_date_in_the_past_is_refused()
        => Assert.Equal("The end date must be today or later",
            Assert.Throws<DomainException>(() => Invite(UserRole.Consultant, until: Today.AddDays(-1))).Message);

    [Fact]
    public void Today_is_a_valid_end_date()
        => Assert.Equal(Today, Invite(UserRole.Auditor, until: Today).AccessUntil);

    [Theory]
    [InlineData(UserRole.Developer)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.ComplianceManager)]
    public void An_employee_role_has_no_end_date(UserRole role)
        => Assert.Equal("An end date is only for a consultant or an auditor",
            Assert.Throws<DomainException>(() => Invite(role, until: Today.AddDays(30))).Message);

    [Fact]
    public void Only_an_auditor_is_limited_to_one_project()
        => Assert.Equal("Only an auditor is limited to one project",
            Assert.Throws<DomainException>(() => Invite(UserRole.Consultant, project: Guid.NewGuid())).Message);

    // ---------------------------------------------------------- Only the invited person (R4)

    [Fact]
    public void The_invited_person_may_use_it_whatever_the_case_of_the_e_mail()
        => Invite(UserRole.Consultant).EnsureFor("Carl@Advisor.example");

    [Fact]
    public void Another_person_may_not_use_it()
        => Assert.Equal("This invitation is for carl@advisor.example",
            Assert.Throws<DomainException>(() => Invite(UserRole.Consultant).EnsureFor("erik@example.com")).Message);

    [Fact]
    public void A_link_without_an_e_mail_is_for_anyone()
        => Invite(UserRole.Developer, email: null).EnsureFor("erik@example.com");

    // ---------------------------------------------------------- The member's access (R2, R3, R5)

    private static (Organization Acme, User Anna, User Carl) Acme(UserRole role = UserRole.Consultant)
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var carl = acme.AddMember(new UserIdentity("carl-sub", "Carl", "carl@advisor.example"), role);
        return (acme, acme.Members.Single(u => u.Name == "Anna"), carl);
    }

    [Fact]
    public void An_accepted_end_date_starts_today()
    {
        var (acme, _, carl) = Acme();

        acme.LimitMember(carl.Id, Invite(UserRole.Consultant, until: Today.AddDays(45)), Today);

        Assert.Equal(Today, carl.AccessFrom);
        Assert.Equal(Today.AddDays(45), carl.AccessUntil);
        Assert.Null(carl.OnlyProjectId);
    }

    [Fact]
    public void An_auditor_with_a_project_but_no_end_date_keeps_the_project_without_an_end()
    {
        var (acme, _, carl) = Acme(UserRole.Auditor);
        var project = Guid.NewGuid();

        acme.LimitMember(carl.Id, Invite(UserRole.Auditor, project: project), Today);

        Assert.Equal(project, carl.OnlyProjectId);
        Assert.Equal(DateOnly.MaxValue, carl.AccessUntil);
    }

    [Fact]
    public void An_invitation_without_limits_clears_the_limits_of_a_member_invited_again()
    {
        var (acme, _, carl) = Acme();
        acme.LimitMember(carl.Id, Invite(UserRole.Consultant, until: Today.AddDays(5)), Today);

        acme.LimitMember(carl.Id, Invite(UserRole.Consultant), Today);

        Assert.Null(carl.AccessFrom);
        Assert.Null(carl.AccessUntil);
    }

    [Fact]
    public void The_admin_moves_the_end_date_and_the_change_says_from_and_to()
    {
        var (acme, anna, carl) = Acme();
        acme.LimitMember(carl.Id, Invite(UserRole.Consultant, until: new DateOnly(2026, 12, 31)), Today);

        var change = acme.SetAccessUntil(carl.Id, new DateOnly(2027, 1, 31), anna.Id, Today);

        Assert.Equal("Access until: 31-12-2026 → 31-01-2027", change!.ToString());
        Assert.Equal(new DateOnly(2027, 1, 31), carl.AccessUntil);
    }

    [Fact]
    public void The_same_end_date_is_no_change()
    {
        var (acme, anna, carl) = Acme();
        acme.SetAccessUntil(carl.Id, Today.AddDays(3), anna.Id, Today);

        Assert.Null(acme.SetAccessUntil(carl.Id, Today.AddDays(3), anna.Id, Today));
    }

    [Fact]
    public void A_consultant_without_an_end_date_gets_one_from_today()
    {
        var (acme, anna, carl) = Acme();

        var change = acme.SetAccessUntil(carl.Id, Today.AddDays(10), anna.Id, Today);

        Assert.Equal("No end date", change!.OldValue);
        Assert.Equal(Today, carl.AccessFrom);
    }

    [Fact]
    public void An_employee_gets_no_end_date()
    {
        var (acme, anna, carl) = Acme(UserRole.Developer);

        Assert.Throws<DomainException>(() => acme.SetAccessUntil(carl.Id, Today.AddDays(10), anna.Id, Today));
    }

    [Fact]
    public void An_end_date_in_the_past_cannot_be_set()
    {
        var (acme, anna, carl) = Acme();

        Assert.Throws<DomainException>(() => acme.SetAccessUntil(carl.Id, Today.AddDays(-1), anna.Id, Today));
    }

    [Fact]
    public void Only_an_admin_sets_the_end_date()
    {
        var (acme, _, carl) = Acme();

        Assert.Throws<DomainException>(() => acme.SetAccessUntil(carl.Id, Today.AddDays(10), carl.Id, Today));
    }

    [Fact]
    public void A_consultant_who_becomes_an_employee_loses_the_end_date()
    {
        var (acme, anna, carl) = Acme();
        acme.SetAccessUntil(carl.Id, Today.AddDays(10), anna.Id, Today);

        acme.ChangeRole(carl.Id, UserRole.Developer, anna.Id);

        Assert.Null(carl.AccessUntil);
        Assert.Null(carl.AccessFrom);
    }
}
