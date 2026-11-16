using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-182: edge cases of an external auditor's time-limited access. Arrange / Act / Assert.
public class AuditorGrantTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Project = Guid.NewGuid();

    private static AuditorGrant Grant(DateOnly? from, DateOnly? until, string email = "eve@audit.example")
        => AuditorGrant.Create(Guid.NewGuid(), Project, email, from, until, Today, Guid.NewGuid(), Guid.NewGuid(), Now);

    // ---------------------------------------------------------- The grant (R1)

    [Fact]
    public void Thirty_days_is_allowed_and_thirty_one_is_not()
    {
        Assert.Equal(Today.AddDays(29), Grant(Today, Today.AddDays(29)).Until);
        Assert.Throws<DomainException>(() => Grant(Today, Today.AddDays(30)));
    }

    [Fact]
    public void A_grant_cannot_start_in_the_past_or_end_before_it_starts()
    {
        Assert.Throws<DomainException>(() => Grant(Today.AddDays(-1), Today));
        Assert.Throws<DomainException>(() => Grant(Today.AddDays(5), Today.AddDays(4)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    public void A_grant_needs_an_e_mail_address(string email)
        => Assert.Throws<DomainException>(() => Grant(Today, Today, email));

    [Fact]
    public void A_grant_is_active_only_in_its_period_and_not_after_revoking()
    {
        var grant = Grant(Today.AddDays(1), Today.AddDays(3));
        Assert.False(grant.IsActiveOn(Today));
        Assert.True(grant.IsActiveOn(Today.AddDays(3)));
        Assert.False(grant.IsActiveOn(Today.AddDays(4)));

        grant.Revoke(Now);
        Assert.False(grant.IsActiveOn(Today.AddDays(2)));
        Assert.Throws<DomainException>(() => grant.Revoke(Now));
    }

    // ---------------------------------------------------------- The membership (R1, R3, R5)

    [Fact]
    public void A_limited_membership_is_open_only_in_its_period()
    {
        var membership = new Membership(Guid.NewGuid(), "Acme ApS", UserRole.Auditor) { OnlyProjectId = Project, AccessFrom = Today, AccessUntil = Today.AddDays(14) };

        Assert.True(membership.IsOpenOn(Today));
        Assert.False(membership.IsOpenOn(Today.AddDays(15)));
        Assert.False(membership.IsOpenOn(Today.AddDays(-1)));
        Assert.True(new Membership(Guid.NewGuid(), "Beta A/S", UserRole.Admin).IsOpenOn(Today.AddYears(5)));
    }

    private static (Organization Organization, User Anna, User Eve, User Bo) Organisation()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna", "Anna", "anna@acme.example"));
        var eve = organization.AddMember(new UserIdentity("eve", "Eve", "eve@audit.example"), UserRole.Auditor);
        var bo = organization.AddMember(new UserIdentity("bo", "Bo", "bo@acme.example"), UserRole.Developer);
        return (organization, organization.Members.First(u => u.Role == UserRole.Admin), eve, bo);
    }

    [Fact]
    public void Only_an_auditor_is_limited_to_a_project()
    {
        var (organization, _, eve, bo) = Organisation();
        var grant = Grant(Today, Today.AddDays(7));

        organization.LimitMember(eve.Id, grant);

        Assert.Equal(Project, eve.OnlyProjectId);
        Assert.Throws<DomainException>(() => organization.LimitMember(bo.Id, grant));
    }

    [Fact]
    public void Ending_the_access_needs_the_right_to_decide_compliance_and_a_limited_member()
    {
        var (organization, anna, eve, bo) = Organisation();
        organization.LimitMember(eve.Id, Grant(Today, Today.AddDays(7)));

        Assert.Throws<DomainException>(() => organization.EndAuditorAccess(eve.Id, bo.Id, Now));     // a developer may not
        Assert.Throws<DomainException>(() => organization.EndAuditorAccess(bo.Id, anna.Id, Now));    // not a limited member
        Assert.False(organization.EndAuditorAccess(eve.Id, anna.Id, Now).IsActive);
    }
}
