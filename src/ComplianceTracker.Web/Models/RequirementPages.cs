using ComplianceTracker.Application.Compliance;

namespace ComplianceTracker.Web.Models
{
    // US-153: one requirement's own page at its address (US-144 R12), e.g. /Projects/Details/{id}/compliance/cra/I.2(d).
    // Pure functions, so the edge cases are tested on their own.
    public static class RequirementPages
    {
        // A requirement code is short; anything longer is not one
        public const int MaxCodeLength = ComplianceAddresses.MaxCodeLength;

        // The code a form sent back ("req"), so the action ends on the same requirement page (R4). Blank or too long: none.
        public static string? Clean(string? code) => ComplianceAddresses.Clean(code);

        // R3: the requirement the address points at (US-232: the rule lives in Application, with the page's loader)
        public static (FrameworkCompliance Framework, RequirementCoverage Row)? Find(ProjectCompliance? compliance, string? sub, string? code)
            => ComplianceAddresses.Find(compliance, sub, code);

        // R1: the status a row and the page show, in one place
        public static string Status(RequirementCoverage row)
            => !row.IsApplicable ? "Not applicable"
                : row.IsCoveredNeedingReview ? "Covered, needs review"
                : row.IsCovered ? "Covered"
                // US-204 R9: in the organisation area, documentation alone takes the place of "Covered"
                : row.EvidenceCovers && row.IsDocumentedOnly ? RequirementCoverage.DocumentedProofMissing
                : row.NeedsAction is not null ? "Needs action" // US-155
                : "Not covered";

        // R1: the badge colour of that status. US-168 R2: from the shared status vocabulary, so it is the same everywhere.
        public static string StatusClass(RequirementCoverage row)
            => StatusBadges.ClassOf(Status(row)) + (row.IsApplicable && !row.IsCovered && !row.IsCoveredNeedingReview && row.NeedsAction is not null ? " needs-action" : "");
    }

    // US-155: the texts of "needs action"
    public static class NeedsActionTexts
    {
        // R2: what one press of "Find what needs action" found
        public static string Found(int needsAction, int doesNotApply)
        {
            if (needsAction == 0 && doesNotApply == 0)
            {
                return "The AI found nothing new that needs action";
            }

            var parts = new List<string>();
            if (needsAction > 0)
            {
                parts.Add($"{needsAction} {(needsAction == 1 ? "requirement needs" : "requirements need")} action");
            }

            if (doesNotApply > 0)
            {
                parts.Add($"{doesNotApply} {(doesNotApply == 1 ? "does" : "do")} not seem to apply");
            }

            return string.Join(" and ", parts) + ". Decide on each one.";
        }

        // R3: the priority's badge colour
        public static string PriorityClass(Domain.Mappings.NeedsActionPriority priority) => priority switch
        {
            Domain.Mappings.NeedsActionPriority.High => "text-bg-danger",
            Domain.Mappings.NeedsActionPriority.Medium => "text-bg-warning",
            _ => "text-bg-secondary"
        };
    }
}
