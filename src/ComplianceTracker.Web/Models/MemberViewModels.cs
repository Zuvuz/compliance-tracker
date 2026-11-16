using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // The Users tab: the members and, for the organisation admin, the open invitations (US-009, US-010)
    // US-196: Projects are the ones an auditor's invitation can be limited to; Today is the earliest end date
    public record MembersViewModel(Organization Organization, IReadOnlyList<Invitation> Invitations)
    {
        public IReadOnlyList<Domain.Projects.Project> Projects { get; init; } = [];
        public DateOnly Today { get; init; }
    }

    // The invitation page (US-009)
    public record InvitationViewModel(string Token, ComplianceTracker.Application.Organizations.InvitationPreview Preview);
}
