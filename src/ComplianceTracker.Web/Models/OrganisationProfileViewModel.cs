using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // US-027: the organisation's details and whether the signed-in member may edit them (the Admin). US-163: the industry
    // and what it suggests, the sector being chosen (with the industry it suggests) and the industries whose own
    // suggestions are coming.
    public sealed record OrganisationProfileViewModel(Organization Organization, bool CanEdit)
    {
        public IndustryView? Industry { get; init; }

        public Sector ChosenSector { get; init; }

        public IReadOnlyDictionary<Industry, string> ComingSoon { get; init; } = new Dictionary<Industry, string>();

        // R2: e.g. "Health" -> Medtech; null when the sector gives nothing to go by
        public Industry? SuggestedIndustry => IndustryFromSector.Suggest(ChosenSector);
    }
}
