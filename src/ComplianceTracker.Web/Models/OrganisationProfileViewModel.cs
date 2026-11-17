using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // US-027: the organisation's details and whether the signed-in member may edit them (the Admin)
    public sealed record OrganisationProfileViewModel(Organization Organization, bool CanEdit);
}
