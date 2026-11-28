using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Web.Models
{
    // US-229: the Risks tab (US-042) on a project and in the organisation area
    public class RisksProjectTab : IProjectTab
    {
        private readonly IProjectTabLoader<RiskTabRequest, RiskTabDto> _loader;

        public RisksProjectTab(IProjectTabLoader<RiskTabRequest, RiskTabDto> loader)
        {
            _loader = loader;
        }

        public string Slug => ProjectTabs.Risks;
        public ProjectTabAreas Areas => ProjectTabAreas.Both;

        public async Task<ProjectTabView?> LoadAsync(ProjectTabContext context, CancellationToken cancellationToken)
        {
            var request = RequestFrom(context);
            return await _loader.LoadAsync(context.Shell.Project.Id, request, cancellationToken) is { } tab
                ? new ProjectTabView("_RisksTab", new RiskTabViewModel(context.Shell, tab, request))
                : null;
        }

        // R8, US-147, US-159 R4, US-203: the filters in the address; an unknown cell, matrix or risk is no filter
        public static RiskTabRequest RequestFrom(ProjectTabContext context)
            => new(
                context.Value("status"),
                context.Value("level"),
                context.Value("due") == "1",
                context.Value("nothing") == "1",
                RiskCells.Parse(context.Value("cell")),
                RiskMatrix.IsAfter(context.Value("matrix")),
                Guid.TryParse(context.Value("risk"), out var risk) ? risk : null);
    }

    // US-229: the model of _RisksTab.cshtml
    public record RiskTabViewModel(ProjectShellView Shell, RiskTabDto Tab, RiskTabRequest Request)
    {
        public Project Project => Shell.Project;
        public Organization Organization => Shell.Organization;
        public IReadOnlyList<Risk> Risks => Tab.Shown;
        public IReadOnlyList<Risk> AllRisks => Tab.All;
        public IReadOnlyDictionary<Guid, RiskBacking> RiskBacking => Tab.Backing;
        public IReadOnlyList<RiskDraft> RiskDrafts => Tab.Drafts;
        public Guid? RiskInPanel => Tab.RiskInPanel;
        public DateTime Now => Tab.Now;
        public string? RiskStatusFilter => Request.Status;
        public string? RiskLevelFilter => Request.Level;
        public bool OnlyReviewDue => Request.OnlyReviewDue;
        public bool OnlyNothingBehind => Request.OnlyNothingBehind;
        public (int Likelihood, int Impact)? RiskCell => Request.Cell;
        public bool RiskMatrixAfter => Request.MatrixAfter;
    }
}
