using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Users;

// Unit tests of roles, permissions and invitations (US-009, US-010). Every test follows Arrange / Act / Assert.
public class RoleTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Organization CreateOrganization()
        => Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));

    [Theory]
    [InlineData(UserRole.Admin, Permission.ManageMembers, true)]
    [InlineData(UserRole.ComplianceManager, Permission.ManageMembers, false)]
    [InlineData(UserRole.ComplianceManager, Permission.DecideCompliance, true)]
    [InlineData(UserRole.Developer, Permission.DecideCompliance, false)]
    [InlineData(UserRole.Developer, Permission.LinkWork, true)]
    [InlineData(UserRole.Developer, Permission.ManageFrameworks, false)]
    [InlineData(UserRole.Auditor, Permission.LinkWork, false)]
    public void The_permission_table(UserRole role, Permission permission, bool allowed)
    {
        // Arrange (nothing)

        // Act
        var can = RolePermissions.Can(role, permission);

        // Assert
        Assert.Equal(allowed, can);
    }

    [Fact]
    public void The_Auditor_may_change_nothing()
    {
        // Arrange (nothing)

        // Act
        var permissions = RolePermissions.Of(UserRole.Auditor);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public void An_Admin_changes_a_role_and_gets_the_change()
    {
        // Arrange
        var organization = CreateOrganization();
        var anna = organization.Users.Single();
        var bo = organization.AddMember(new UserIdentity("bo-sub", "Bo", "bo@acme.test"), UserRole.Developer);

        // Act
        var change = organization.ChangeRole(bo.Id, UserRole.ComplianceManager, anna.Id);

        // Assert
        Assert.Equal("Bo: Developer → Compliance manager", change!.ToString());
        Assert.Equal(UserRole.ComplianceManager, bo.Role);
    }

    [Fact]
    public void Only_an_Admin_changes_roles()
    {
        // Arrange
        var organization = CreateOrganization();
        var anna = organization.Users.Single();
        var bo = organization.AddMember(new UserIdentity("bo-sub", "Bo", "bo@acme.test"), UserRole.ComplianceManager);

        // Act
        var error = Assert.Throws<DomainException>(() => organization.ChangeRole(anna.Id, UserRole.Auditor, bo.Id));

        // Assert
        Assert.Equal("Your role does not allow this", error.Message);
        Assert.Equal(UserRole.Admin, anna.Role);
    }

    [Fact]
    public void The_last_Admin_cannot_be_demoted_also_not_by_the_system_admin()
    {
        // Arrange
        var organization = CreateOrganization();
        var anna = organization.Users.Single();

        // Act
        var error = Assert.Throws<DomainException>(() => organization.ChangeRoleAsSystemAdmin(anna.Id, UserRole.Developer));

        // Assert
        Assert.Equal("The organisation must keep at least one organisation admin", error.Message);
    }

    [Fact]
    public void A_person_is_a_member_once()
    {
        // Arrange
        var organization = CreateOrganization();

        // Act
        var error = Assert.Throws<DomainException>(() => organization.AddMember(new UserIdentity("anna-sub", "Anna", "anna@acme.test"), UserRole.Auditor));

        // Assert
        Assert.Equal("You are already a member of this organisation", error.Message);
    }

    [Fact]
    public void An_invitation_works_once_and_for_seven_days()
    {
        // Arrange
        var invitation = Invitation.Create(Guid.NewGuid(), UserRole.Developer, "hash", Guid.NewGuid(), Monday);

        // Act
        var usableDay6 = invitation.IsUsable(Monday.AddDays(6));
        var expired = Assert.Throws<DomainException>(() => invitation.EnsureUsable(Monday.AddDays(7)));
        invitation.MarkAccepted(Guid.NewGuid(), Monday.AddDays(1));
        var used = Assert.Throws<DomainException>(() => invitation.EnsureUsable(Monday.AddDays(2)));

        // Assert
        Assert.True(usableDay6);
        Assert.Equal("The invitation has expired", expired.Message);
        Assert.Equal("The invitation has already been used", used.Message);
    }

    [Fact]
    public void A_revoked_invitation_cannot_be_used()
    {
        // Arrange
        var invitation = Invitation.Create(Guid.NewGuid(), UserRole.Developer, "hash", Guid.NewGuid(), Monday);

        // Act
        invitation.Revoke(Monday);
        var error = Assert.Throws<DomainException>(() => invitation.MarkAccepted(Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("The invitation has been revoked", error.Message);
    }
}
