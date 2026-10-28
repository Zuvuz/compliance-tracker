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

    // Data for the page with all versions of one framework (Views/Frameworks/Versions.cshtml)
    public record FrameworkVersionsViewModel(Framework Framework, Organization Organization)
    {
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }
}
