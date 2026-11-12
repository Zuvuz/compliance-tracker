using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Organizations;

// Unit tests of the erasure of a removed member's personal data (US-095 R3). Every test follows Arrange / Act / Assert.
public class PersonalDataTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static (Organization Acme, User Anna, User Carl) Acme()
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var carl = acme.AddMember(new UserIdentity("carl-sub", "Carl", "carl@acme.test"), UserRole.Developer);
        return (acme, acme.Members.Single(u => u.Name == "Anna"), carl);
    }

    [Fact]
    public void A_removed_member_gets_a_pseudonym_and_loses_the_link_to_Keycloak()
    {
        var (acme, anna, carl) = Acme();
        acme.RemoveMember(carl.Id, anna.Id, Monday);

        acme.ErasePersonalData(carl.Id, anna.Id);

        Assert.StartsWith("Former member ", carl.Name);
        Assert.Equal(carl.Pseudonym, carl.Name);
        Assert.DoesNotContain("carl", carl.Email, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(User.ErasedPrefix, carl.ExternalId);
        Assert.True(carl.IsErased);
    }

    [Fact]
    public void The_member_keeps_their_id_so_the_history_points_at_the_pseudonym()
    {
        var (acme, anna, carl) = Acme();
        var id = carl.Id;
        acme.RemoveMember(carl.Id, anna.Id, Monday);

        acme.ErasePersonalData(carl.Id, anna.Id);

        Assert.Equal(id, acme.Users.Single(u => u.IsErased).Id);
        Assert.Equal(Monday, carl.RemovedAt);
    }

    [Fact]
    public void An_active_member_cannot_be_erased()
    {
        var (acme, anna, carl) = Acme();

        Assert.Equal("Remove the member first", Assert.Throws<DomainException>(() => acme.ErasePersonalData(carl.Id, anna.Id)).Message);
        Assert.Equal("Carl", carl.Name);
    }

    [Fact]
    public void A_member_is_erased_only_once()
    {
        var (acme, anna, carl) = Acme();
        acme.RemoveMember(carl.Id, anna.Id, Monday);
        acme.ErasePersonalData(carl.Id, anna.Id);

        Assert.Throws<DomainException>(() => acme.ErasePersonalData(carl.Id, anna.Id));
    }

    [Fact]
    public void Only_who_manages_members_can_erase()
    {
        var (acme, anna, carl) = Acme();
        var dan = acme.AddMember(new UserIdentity("dan-sub", "Dan", "dan@acme.test"), UserRole.Developer);
        acme.RemoveMember(carl.Id, anna.Id, Monday);

        Assert.Equal("Your role does not allow this", Assert.Throws<DomainException>(() => acme.ErasePersonalData(carl.Id, dan.Id)).Message);
    }

    [Fact]
    public void An_unknown_member_is_not_found()
    {
        var (acme, anna, _) = Acme();

        Assert.Equal("Member not found", Assert.Throws<DomainException>(() => acme.ErasePersonalData(Guid.NewGuid(), anna.Id)).Message);
    }

    [Fact]
    public void The_pseudonyms_of_two_members_differ()
    {
        var (acme, anna, carl) = Acme();
        var dan = acme.AddMember(new UserIdentity("dan-sub", "Dan", "dan@acme.test"), UserRole.Developer);

        Assert.NotEqual(carl.Pseudonym, dan.Pseudonym);
        Assert.Equal(18, carl.Pseudonym.Length); // "Former member " and four characters
    }
}
