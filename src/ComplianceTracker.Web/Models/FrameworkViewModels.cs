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
    public record FrameworkVersionPageViewModel(Framework Framework, FrameworkVersion Version, Organization Organization, DateOnly Today)
    {
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
}
