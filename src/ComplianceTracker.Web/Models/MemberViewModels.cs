using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // The Users tab: the members and, for the organisation admin, the open invitations (US-009, US-010)
    public record MembersViewModel(Organization Organization, IReadOnlyList<Invitation> Invitations);

    // The invitation page (US-009)
    public record InvitationViewModel(string Token, ComplianceTracker.Application.Organizations.InvitationPreview Preview);
}
