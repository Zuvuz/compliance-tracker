using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Ai;

// Edge cases of which decisions may be used to improve the AI (US-130 R5). Arrange / Act / Assert.
public class AiSharingTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Organization Organization()
        => Domain.Organizations.Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));

    private static MappingSuggestion Decided(Guid projectId, DateTime at, bool approve = true)
    {
        var suggestion = MappingSuggestion.Create(projectId, Guid.NewGuid(), Guid.NewGuid(), 0.8, 1, "m", "mapping-v3", at.AddMinutes(-1));
        if (approve)
        {
            suggestion.Approve(Guid.NewGuid(), at);
        }

        return suggestion;
    }

    [Fact]
    public void A_new_organisation_shares_every_decision()
    {
        // Arrange
        var organization = Organization();

        // Act
        var may = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday));

        // Assert
        Assert.True(may);
    }

    [Fact]
    public void Nothing_is_used_while_sharing_is_off()
    {
        // Arrange
        var organization = Organization();
        organization.SetShareAiFeedback(false, organization.Users.Single().Id, Monday);

        // Act
        var may = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday.AddDays(1)));

        // Assert
        Assert.False(may);
    }

    [Fact]
    public void After_switching_on_again_only_later_decisions_are_used_and_the_moment_itself_counts()
    {
        // Arrange
        var organization = Organization();
        var anna = organization.Users.Single().Id;
        organization.SetShareAiFeedback(false, anna, Monday);
        organization.SetShareAiFeedback(true, anna, Monday.AddDays(2));

        // Act
        var before = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday.AddDays(1)));
        var atTheMoment = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday.AddDays(2)));
        var after = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday.AddDays(3)));

        // Assert
        Assert.False(before);
        Assert.True(atTheMoment);
        Assert.True(after);
    }

    [Fact]
    public void A_project_that_is_left_out_is_never_used()
    {
        // Arrange
        var organization = Organization();
        var project = Guid.NewGuid();

        // Act
        var may = AiSharing.MayUse(organization, new HashSet<Guid> { project }, Decided(project, Monday));

        // Assert
        Assert.False(may);
    }

    [Fact]
    public void An_undecided_suggestion_is_never_used()
    {
        // Arrange
        var organization = Organization();

        // Act
        var may = AiSharing.MayUse(organization, new HashSet<Guid>(), Decided(Guid.NewGuid(), Monday, approve: false));

        // Assert
        Assert.False(may);
    }

    [Fact]
    public void Switching_on_when_already_on_does_not_move_the_start()
    {
        // Arrange
        var organization = Organization();
        var anna = organization.Users.Single().Id;

        // Act
        var changed = organization.SetShareAiFeedback(true, anna, Monday.AddDays(5));

        // Assert
        Assert.False(changed);
        Assert.Null(organization.ShareAiFeedbackSince);
    }
}
