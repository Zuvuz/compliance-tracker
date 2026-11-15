using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-187: edge cases of working in several organisations. Arrange / Act / Assert.
public class ConsultantAccessTests
{
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Beta = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static Invitation Invite(Guid organisation, UserRole role) => Invitation.Create(organisation, role, "hash", Guid.NewGuid(), Monday);

    // ---------------------------------------------------------- Roles (R1)

    [Theory]
    [InlineData(UserRole.Consultant, true)]
    [InlineData(UserRole.Auditor, true)]
    [InlineData(UserRole.Admin, false)]
    [InlineData(UserRole.ComplianceManager, false)]
    [InlineData(UserRole.Developer, false)]
    public void Only_consultants_and_auditors_are_external(UserRole role, bool external)
        => Assert.Equal(external, RolePermissions.IsExternal(role));

    [Fact]
    public void A_consultant_may_do_the_compliance_work_but_not_manage_members_or_ai_sharing()
    {
        Assert.True(RolePermissions.Can(UserRole.Consultant, Permission.DecideCompliance));
        Assert.True(RolePermissions.Can(UserRole.Consultant, Permission.ManageRisks));
        Assert.False(RolePermissions.Can(UserRole.Consultant, Permission.ManageMembers));
        Assert.False(RolePermissions.Can(UserRole.Consultant, Permission.ManageAiSharing));
        Assert.Equal("Consultant", RolePermissions.Title(UserRole.Consultant));
    }

    // ---------------------------------------------------------- Joining (R1)

    [Fact]
    public void A_person_in_no_organisation_may_join_with_any_role()
        => Assert.Null(MemberService.Refusal([], Invite(Acme, UserRole.Developer)));

    [Fact]
    public void A_member_of_another_organisation_may_join_as_consultant_or_auditor()
    {
        var memberships = new[] { new Membership(Beta, "Beta A/S", UserRole.Admin) };

        Assert.Null(MemberService.Refusal(memberships, Invite(Acme, UserRole.Consultant)));
        Assert.Null(MemberService.Refusal(memberships, Invite(Acme, UserRole.Auditor)));
    }

    [Fact]
    public void A_member_of_another_organisation_may_not_join_as_an_employee()
        => Assert.Equal("You are already a member of an organisation",
            MemberService.Refusal([new Membership(Beta, "Beta A/S", UserRole.Consultant)], Invite(Acme, UserRole.Developer)));

    [Fact]
    public void A_member_cannot_join_the_same_organisation_twice()
        => Assert.Equal("You are already a member of this organisation",
            MemberService.Refusal([new Membership(Acme, "Acme ApS", UserRole.Consultant)], Invite(Acme, UserRole.Consultant)));

    // ---------------------------------------------------------- Choosing (R2)

    [Fact]
    public void The_chosen_organisation_is_used_when_the_person_is_a_member()
    {
        var memberships = new[] { new Membership(Acme, "Acme ApS", UserRole.Consultant), new Membership(Beta, "Beta A/S", UserRole.Consultant) };

        Assert.Equal(Beta, CurrentMember.Choose(memberships, Beta)!.OrganizationId);
    }

    [Fact]
    public void A_chosen_organisation_the_person_is_not_a_member_of_falls_back_to_the_first()
    {
        var memberships = new[] { new Membership(Acme, "Acme ApS", UserRole.Admin) };

        Assert.Equal(Acme, CurrentMember.Choose(memberships, Guid.NewGuid())!.OrganizationId);
        Assert.Equal(Acme, CurrentMember.Choose(memberships, null)!.OrganizationId);
    }

    [Fact]
    public void No_memberships_choose_nothing()
        => Assert.Null(CurrentMember.Choose([], Acme));
}
