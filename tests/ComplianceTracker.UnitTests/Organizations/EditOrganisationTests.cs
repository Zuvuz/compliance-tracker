using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-027: Organization.Update: the rules from Create, only the Admin, the CVR unchanged, and the changes for the log.
// Edge cases. Arrange / Act / Assert.
public class EditOrganisationTests
{
    [Fact]
    public void Each_change_is_returned_with_the_old_and_new_value()
    {
        var (acme, anna, _) = Acme();

        var changes = acme.Update("Acme Holding ApS", Sector.Health, OrganizationSize.Medium, anna);

        Assert.Equal(["Name: Acme ApS → Acme Holding ApS", "Sector: Manufacturing → Health", "Size: Small → Medium"], changes.Select(c => c.ToString()));
        Assert.Equal(("Acme Holding ApS", Sector.Health, OrganizationSize.Medium), (acme.Name, acme.Sector, acme.Size));
    }

    [Fact]
    public void Nothing_changed_gives_no_changes()
    {
        var (acme, anna, _) = Acme();

        Assert.Empty(acme.Update("Acme ApS", Sector.Manufacturing, OrganizationSize.Small, anna));
    }

    [Fact]
    public void The_name_is_trimmed_so_spaces_alone_are_no_change()
    {
        var (acme, anna, _) = Acme();

        Assert.Empty(acme.Update("  Acme ApS  ", Sector.Manufacturing, OrganizationSize.Small, anna));
        Assert.Equal("Acme ApS", acme.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void The_name_is_still_required(string? name)
    {
        var (acme, anna, _) = Acme();

        Assert.Equal("Name is required", Assert.Throws<DomainException>(() => acme.Update(name, Sector.Health, OrganizationSize.Small, anna)).Message);
        Assert.Equal(Sector.Manufacturing, acme.Sector); // nothing was changed
    }

    [Fact]
    public void A_name_longer_than_200_characters_is_refused()
    {
        var (acme, anna, _) = Acme();

        Assert.Throws<DomainException>(() => acme.Update(new string('a', Organization.MaxNameLength + 1), Sector.Health, OrganizationSize.Small, anna));

        // Exactly 200 is fine, also with spaces around it
        acme.Update(" " + new string('a', Organization.MaxNameLength) + " ", Sector.Manufacturing, OrganizationSize.Small, anna);
        Assert.Equal(Organization.MaxNameLength, acme.Name.Length);
    }

    [Fact]
    public void Sector_and_size_are_required_and_must_be_known()
    {
        var (acme, anna, _) = Acme();

        Assert.Equal("Sector is required", Assert.Throws<DomainException>(() => acme.Update("Acme ApS", null, OrganizationSize.Small, anna)).Message);
        Assert.Equal("Size is required", Assert.Throws<DomainException>(() => acme.Update("Acme ApS", Sector.Health, null, anna)).Message);
        Assert.Throws<DomainException>(() => acme.Update("Acme ApS", (Sector)999, OrganizationSize.Small, anna));
        Assert.Throws<DomainException>(() => acme.Update("Acme ApS", Sector.Health, (OrganizationSize)999, anna));
    }

    [Fact]
    public void Only_an_admin_can_edit_and_the_cvr_never_changes()
    {
        var (acme, _, developer) = Acme();

        Assert.Equal("Only an Admin can edit the organisation",
            Assert.Throws<DomainException>(() => acme.Update("Other", Sector.Health, OrganizationSize.Large, developer)).Message);
        Assert.Throws<DomainException>(() => acme.Update("Other", Sector.Health, OrganizationSize.Large, Guid.NewGuid()));
        Assert.Equal("12345678", acme.Cvr);
        Assert.Equal("Acme ApS", acme.Name);
    }

    private static (Organization Organization, Guid AdminId, Guid DeveloperId) Acme()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@example.com"));
        var developer = organization.AddMember(new UserIdentity("dan-sub", "Dan", "dan@example.com"), UserRole.Developer);
        return (organization, organization.Users[0].Id, developer.Id);
    }
}
