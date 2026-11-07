using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Approvals;

// Edge-case unit tests of US-135 in Application: who else may approve, and the payloads kept with a request.
// Arrange / Act / Assert.
public class ApprovalRulesTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static (Organization Organization, User Anna) Organisation()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Energy, OrganizationSize.Small, new UserIdentity("anna", "Anna", "anna@acme.test"));
        return (organization, organization.Users.Single());
    }

    private static ApprovalRequest Request(object payload)
        => ApprovalRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ApprovalAction.AcceptRisk, null, null, "Something",
            System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            Guid.NewGuid(), Monday);

    [Fact]
    public void No_one_else_may_approve_in_an_organisation_of_one()
    {
        // Arrange
        var (organization, anna) = Organisation();

        // Act / Assert
        Assert.False(ApprovalService.AnyoneElseMayApprove(organization, anna.Id, Permission.DecideCompliance));
    }

    [Fact]
    public void A_member_whose_role_lacks_the_permission_does_not_count()
    {
        // Arrange
        var (organization, anna) = Organisation();
        organization.AddMember(new UserIdentity("dan", "Dan", "dan@acme.test"), UserRole.Developer);

        // Act / Assert
        Assert.False(ApprovalService.AnyoneElseMayApprove(organization, anna.Id, Permission.DecideCompliance));
        Assert.True(ApprovalService.AnyoneElseMayApprove(organization, anna.Id, Permission.ManageRisks));
    }

    [Fact]
    public void A_removed_member_does_not_count()
    {
        // Arrange
        var (organization, anna) = Organisation();
        var bo = organization.AddMember(new UserIdentity("bo", "Bo", "bo@acme.test"), UserRole.ComplianceManager);
        organization.RemoveMember(bo.Id, anna.Id, Monday);

        // Act / Assert
        Assert.False(ApprovalService.AnyoneElseMayApprove(organization, anna.Id, Permission.DecideCompliance));
    }

    [Fact]
    public void The_auditor_never_approves()
    {
        // Arrange
        var (organization, anna) = Organisation();
        organization.AddMember(new UserIdentity("ida", "Ida", "ida@acme.test"), UserRole.Auditor);

        // Act / Assert
        Assert.All(Enum.GetValues<Permission>(), p => Assert.False(ApprovalService.AnyoneElseMayApprove(organization, anna.Id, p)));
    }

    [Fact]
    public void A_risk_payload_reads_back_the_same()
    {
        // Arrange
        var payload = new AcceptRiskPayload(Guid.NewGuid(), "Short-lived tokens, \"quoted\" and æøå");

        // Act
        var read = ApprovalGate.Read<AcceptRiskPayload>(Request(payload));

        // Assert
        Assert.Equal(payload, read);
    }

    [Fact]
    public void An_event_payload_keeps_its_time_and_an_empty_suggestion()
    {
        // Arrange
        var payload = new NotReportableEventPayload(SecurityEventKind.IncidentNotSevere, "Phishing mail", null, Monday, null);

        // Act
        var read = ApprovalGate.Read<NotReportableEventPayload>(Request(payload));

        // Assert
        Assert.Equal(payload, read);
        Assert.Equal(DateTimeKind.Utc, read.AwareAt.Kind);
    }

    [Fact]
    public void An_end_of_support_payload_keeps_its_dates()
    {
        // Arrange
        var payload = new EndSupportPayload(new DateOnly(2026, 12, 31), new DateOnly(2040, 1, 1));

        // Act / Assert
        Assert.Equal(payload, ApprovalGate.Read<EndSupportPayload>(Request(payload)));
    }

    [Fact]
    public void A_payload_that_is_not_an_object_cannot_be_read()
    {
        // Arrange
        var request = ApprovalRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ApprovalAction.AcceptRisk, null, null, "Something", "null", Guid.NewGuid(), Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => ApprovalGate.Read<AcceptRiskPayload>(request));
    }

    [Theory]
    [InlineData(SecurityEventKind.ActivelyExploitedVulnerability, false)]
    [InlineData(SecurityEventKind.SevereIncident, false)]
    [InlineData(SecurityEventKind.VulnerabilityNotExploited, true)]
    [InlineData(SecurityEventKind.IncidentNotSevere, true)]
    public void Only_events_that_are_not_reported_need_approval(SecurityEventKind kind, bool needs)
    {
        // Act / Assert
        Assert.Equal(needs, SecurityEventService.IsNotReportable(kind));
    }
}
