using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // Data for the framework list page (Views/Frameworks/Index.cshtml)
    public record FrameworksPageViewModel(Organization Organization, IReadOnlyList<Framework> Frameworks, DateOnly Today)
    {
        // The name of the member who uploaded a version (a version only stores the user's id)
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }

    // Data for one version's page with its requirements (Views/Frameworks/Version.cshtml, US-007)
    // Columns: US-143 R7, the header row of a draft's Excel or CSV file, so the admin can choose the columns
    public record FrameworkVersionPageViewModel(
        Framework Framework, FrameworkVersion Version, Organization Organization, DateOnly Today, IReadOnlyList<string>? Columns = null,
        Application.Approvals.ApprovalView? PendingActivation = null)
    {
        // US-137 R4: the profile of the standard, checked against the draft's codes as they are now
        public Application.Frameworks.FrameworkProfile? Profile => Application.Frameworks.FrameworkProfiles.For(Framework.Name);

        public IReadOnlyList<CompletenessGroup> ProfileCheck
            => Profile is { } profile && Version.Completeness?.Profile is not null
                ? profile.Check(Version.Requirements.Select(r => r.Code))
                : Version.Completeness?.Groups ?? [];

        // US-143 R7: the columns chosen before for this framework
        public Application.Frameworks.TableColumns? Chosen => Application.Frameworks.TableColumns.Parse(Framework.TableColumns);

        // US-143 R4: how deep a requirement sits in the standard's tree
        public int Depth(Requirement requirement)
        {
            var depth = 0;
            for (var up = requirement.ParentId; up is { } id && depth < 10; up = Version.Requirements.FirstOrDefault(r => r.Id == id)?.ParentId)
            {
                depth++;
            }

            return depth;
        }

        public string? ParentCode(Requirement requirement)
            => requirement.ParentId is { } id ? Version.Requirements.FirstOrDefault(r => r.Id == id)?.Code : null;

        public bool IsCurrent => Framework.CurrentVersion(Today)?.Id == Version.Id;

        public string NameOf(Guid? userId)
            => Organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";
    }

    // Data for the built-in frameworks page (Views/Frameworks/BuiltIn.cshtml, US-006)
    public record BuiltInFrameworksViewModel(IReadOnlyList<Framework> Frameworks, IReadOnlySet<Guid> SwitchedOn)
    {
        public bool IsOn(Framework framework) => SwitchedOn.Contains(framework.Id);
    }

    // Data for the page with all versions of one framework (Views/Frameworks/Versions.cshtml)
    public record FrameworkVersionsViewModel(Framework Framework, Organization Organization, DateOnly Today)
    {
        // US-035: e.g. "Active (current)", "Upcoming (applies from 01-02-2027)" or "Draft"
        public string StatusOf(FrameworkVersion version) => VersionStatus.Text(Framework, version, Today);

        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }

    // The status text of a framework version on a day (US-007, US-035), shared by the framework pages
    public static class VersionStatus
    {
        public static string Text(Framework framework, FrameworkVersion version, DateOnly today)
        {
            if (!version.IsActive)
            {
                return "Draft";
            }

            var current = framework.CurrentVersion(today) == version;
            if (version.IsUpcomingOn(today))
            {
                var from = version.AppliesFrom!.Value.ToString("dd-MM-yyyy");
                return current ? $"Active (current, applies from {from})" : $"Upcoming (applies from {from})";
            }

            return current ? "Active (current)" : "Active";
        }
    }

    // US-116: the AI quality page. Pairs of runs of the same step can be compared (R4).
    public record AiQualityViewModel(
        IReadOnlyList<Domain.Ai.AiEvaluation> Runs,
        IReadOnlyList<Application.Ai.RationaleSample> Sample)
    {
        // R4: e.g. "Top 3: 71 % → 78 % (better)". Higher is better, except rejected matches above the limit.
        public static string Compare(string label, double? before, double? after, bool lowerIsBetter = false)
        {
            if (before is not { } b || after is not { } a)
            {
                return $"{label}: –";
            }

            var verdict = a == b ? "the same" : (a > b) != lowerIsBetter ? "better" : "worse";
            return $"{label}: {b:0.#} → {a:0.#} ({verdict})";
        }
    }
}
