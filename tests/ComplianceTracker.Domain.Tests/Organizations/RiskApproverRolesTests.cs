using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Organizations;

// US-217: the roles that approve accepting High and Critical risks. Every test follows Arrange / Act / Assert.
public class RiskApproverRolesTests
{
    private static Organization Acme()
        => Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));

    [Fact]
    public void Organisation_admin_approves_by_default()
    {
        var acme = Acme();

        Assert.Equal([UserRole.Admin], acme.RiskApproverRoles);
        Assert.True(acme.IsRiskApprover(UserRole.Admin));
        Assert.False(acme.IsRiskApprover(UserRole.ComplianceManager));
    }

    [Fact]
    public void Several_roles_can_approve_and_the_old_ones_are_returned_for_the_log()
    {
        var acme = Acme();

        var old = acme.SetRiskApproverRoles([UserRole.ComplianceManager, UserRole.Admin, UserRole.Admin]);

        Assert.Equal([UserRole.Admin], old);
        Assert.Equal([UserRole.Admin, UserRole.ComplianceManager], acme.RiskApproverRoles);
        Assert.True(acme.IsRiskApprover(UserRole.ComplianceManager));
    }

    [Fact]
    public void At_least_one_role_must_approve()
    {
        var acme = Acme();

        var error = Assert.Throws<DomainException>(() => acme.SetRiskApproverRoles([]));

        Assert.Equal("Choose at least one role that approves accepting High and Critical risks", error.Message);
        Assert.Equal([UserRole.Admin], acme.RiskApproverRoles);
    }

    [Theory]
    [InlineData(UserRole.Developer)]
    [InlineData(UserRole.Auditor)]
    public void A_role_that_may_not_decide_compliance_cannot_approve(UserRole role)
    {
        var acme = Acme();

        var error = Assert.Throws<DomainException>(() => acme.SetRiskApproverRoles([UserRole.Admin, role]));

        Assert.EndsWith("may not decide compliance, so it cannot approve risks", error.Message);
        Assert.Equal([UserRole.Admin], acme.RiskApproverRoles);
    }

    [Fact]
    public void A_role_that_does_not_exist_is_refused()
        => Assert.Throws<DomainException>(() => Acme().SetRiskApproverRoles([(UserRole)42]));

    [Fact]
    public void No_roles_given_is_refused()
        => Assert.Throws<DomainException>(() => Acme().SetRiskApproverRoles(null));
}
