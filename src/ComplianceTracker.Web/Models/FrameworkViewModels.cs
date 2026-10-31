using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // Data for the framework list page (Views/Frameworks/Index.cshtml)
    public record FrameworksPageViewModel(Organization Organization, IReadOnlyList<Framework> Frameworks)
    {
        // The name of the member who uploaded a version (a version only stores the user's id)
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }

    // Data for one version's page with its requirements (Views/Frameworks/Version.cshtml, US-007)
    public record FrameworkVersionPageViewModel(Framework Framework, FrameworkVersion Version, Organization Organization)
    {
        public bool IsCurrent => Framework.CurrentVersion?.Id == Version.Id;

        public string NameOf(Guid? userId)
            => Organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";
    }

    // Data for the built-in frameworks page (Views/Frameworks/BuiltIn.cshtml, US-006)
    public record BuiltInFrameworksViewModel(IReadOnlyList<Framework> Frameworks, IReadOnlySet<Guid> SwitchedOn)
    {
        public bool IsOn(Framework framework) => SwitchedOn.Contains(framework.Id);
    }

    // Data for the page with all versions of one framework (Views/Frameworks/Versions.cshtml)
    public record FrameworkVersionsViewModel(Framework Framework, Organization Organization)
    {
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }
}
