using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // Listen over organisationens frameworks
    public record FrameworksPageViewModel(Organization Organization, IReadOnlyList<Framework> Frameworks)
    {
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }

    // Alle versioner af ét framework
    public record FrameworkVersionsViewModel(Framework Framework, Organization Organization)
    {
        public string UploadedBy(FrameworkVersion version)
            => Organization.Users.FirstOrDefault(u => u.Id == version.UploadedById)?.Name ?? "Unknown";
    }
}