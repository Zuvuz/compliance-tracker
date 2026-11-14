using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Web.Models
{
    // US-144, US-153: what a requirement's overview row (_RequirementSummaryRow.cshtml) and its own page
    // (_RequirementPage.cshtml) need. Sub is the sub-tab the back link goes to ("all" on the All tab).
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
