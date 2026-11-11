using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Web.Models
{
    // US-144: what one requirement row on the Compliance tab needs (Views/Projects/_RequirementRow.cshtml). Sub is the
    // sub-tab the forms go back to ("all" on the All tab); Open is true for the requirement the address points at (R12).
    public record RequirementRowModel(
        Project Project,
        FrameworkCompliance Framework,
        RequirementCoverage Row,
        string? Sub,
        string? ShowFilter,
        bool CanLink,
        bool CanDecide,
        IReadOnlyList<WorkItem> OpenStories,
        IReadOnlyList<EvidenceRow> PickableEvidence,
        IReadOnlyList<Domain.Risks.Risk> ProjectRisks,
        Func<Guid, string> NameOf,
        string? Language,
        bool HasTranslations,
        int Depth,
        bool HasChildren,
        bool Open,
        string? ImplementsName)
    {
        public string Address(Microsoft.AspNetCore.Mvc.IUrlHelper url)
            => Microsoft.AspNetCore.Mvc.UrlHelperExtensions.Action(url, "Details", "Projects", new
            {
                id = Project.Id,
                tab = ProjectTabs.Compliance,
                sub = FrameworkNames.Slug(Framework.Framework.Name),
                req = Row.Requirement.Code
            })!;
    }
}
