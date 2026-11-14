using ComplianceTracker.Application.Compliance;

namespace ComplianceTracker.Web.Models
{
    // US-153: one requirement's own page at its address (US-144 R12), e.g. /Projects/Details/{id}/compliance/cra/I.2(d).
    // Pure functions, so the edge cases are tested on their own.
    public static class RequirementPages
    {
        // A requirement code is short; anything longer is not one
        public const int MaxCodeLength = 100;

        // The code a form sent back ("req"), so the action ends on the same requirement page (R4). Blank or too long: none.
        public static string? Clean(string? code)
        {
            var trimmed = code?.Trim();
            return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxCodeLength ? null : trimmed;
        }

        // R3: the requirement the address points at, in the sub-tab's framework (any framework on "all"), whatever the
        // case of the code. Null when the code or the framework is unknown, so the page is "not found".
        public static (FrameworkCompliance Framework, RequirementCoverage Row)? Find(ProjectCompliance? compliance, string? sub, string? code)
        {
            var wanted = Clean(code);
            if (compliance is null || wanted is null)
            {
                return null;
            }

            var frameworks = sub is null || sub == ComplianceFilters.AllSub
                ? compliance.Frameworks
                : compliance.Frameworks.Where(f => FrameworkNames.Slug(f.Framework.Name) == sub).ToList();
            foreach (var framework in frameworks)
            {
                var row = framework.Requirements.FirstOrDefault(r => string.Equals(r.Requirement.Code, wanted, StringComparison.OrdinalIgnoreCase));
                if (row is not null)
                {
                    return (framework, row);
                }
            }

            return null;
        }

        // R1: the status a row and the page show, in one place
        public static string Status(RequirementCoverage row)
            => !row.IsApplicable ? "Not applicable"
                : row.IsCoveredNeedingReview ? "Covered, needs review"
                : row.IsCovered ? "Covered"
                : "Not covered";

        // R1: the badge colour of that status
        public static string StatusClass(RequirementCoverage row)
            => !row.IsApplicable ? "text-bg-secondary"
                : row.IsCoveredNeedingReview ? "text-bg-warning"
                : row.IsCovered ? "text-bg-success"
                : "text-bg-danger";
    }
}
