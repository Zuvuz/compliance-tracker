using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-184: edge cases of closing an organisation. Arrange / Act / Assert.
public class OrganisationClosingTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static (Organization Organization, User Anna, User Bo) Organisation()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna", "Anna", "anna@acme.example"));
        var bo = organization.AddMember(new UserIdentity("bo", "Bo", "bo@acme.example"), UserRole.Developer);
        return (organization, organization.Members.First(u => u.Role == UserRole.Admin), bo);
    }

    [Fact]
    public void Closing_waits_thirty_days()
    {
        var (organization, anna, _) = Organisation();
        organization.StartClosing(anna.Id, Today);

        Assert.Equal(Today.AddDays(30), organization.ClosesOn);
        Assert.False(organization.IsClosingDue(Today.AddDays(29)));
        Assert.True(organization.IsClosingDue(Today.AddDays(30)));
    }

    [Fact]
    public void Only_an_admin_may_start_or_undo_closing()
    {
        var (organization, anna, bo) = Organisation();

        Assert.Throws<DomainException>(() => organization.StartClosing(bo.Id, Today));
        organization.StartClosing(anna.Id, Today);
        Assert.Throws<DomainException>(() => organization.UndoClosing(bo.Id));
    }

    [Fact]
    public void Closing_twice_is_refused_and_undoing_what_is_not_closing_too()
    {
        var (organization, anna, _) = Organisation();
        Assert.Throws<DomainException>(() => organization.UndoClosing(anna.Id));

        organization.StartClosing(anna.Id, Today);
        Assert.Throws<DomainException>(() => organization.StartClosing(anna.Id, Today));
    }

    [Fact]
    public void Undone_closing_is_never_due()
    {
        var (organization, anna, _) = Organisation();
        organization.StartClosing(anna.Id, Today);
        organization.UndoClosing(anna.Id);

        Assert.False(organization.IsClosing);
        Assert.False(organization.IsClosingDue(Today.AddYears(1)));
    }

    [Fact]
    public void A_closed_organisation_and_its_members_are_anonymised()
    {
        var (organization, anna, _) = Organisation();
        organization.StartClosing(anna.Id, Today);

        organization.Close(Now);

        Assert.StartsWith("Closed organisation ", organization.Name);
        Assert.Equal(8, organization.Cvr.Length);
        Assert.All(organization.Users, u => Assert.True(u.IsErased && !u.IsActive));
        Assert.DoesNotContain(organization.Users, u => u.Email.Contains("acme.example"));
        Assert.False(organization.IsClosing);
    }
}
