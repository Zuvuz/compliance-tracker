using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Compliance
{
    // What the Compliance tab reads (US-019, US-021, US-041): per framework the project follows,
    // the requirements of the version it uses, the user stories linked to each, and the coverage.
    // Only the signed-in user's own projects; others give null.
    public class ComplianceQueries
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly CurrentMember _member;

        public ComplianceQueries(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRequirementLinkRepository links,
            CurrentMember member)
        {
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _member = member;
        }

        public async Task<ProjectCompliance?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(
                organization.Id, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var workItems = (await _workItems.ListAsync(project.Id, cancellationToken)).ToDictionary(w => w.Id);
            var links = await _links.ListActiveAsync(project.Id, cancellationToken);

            var result = new List<FrameworkCompliance>();
            foreach (var used in project.Frameworks)
            {
                var framework = frameworks.FirstOrDefault(f => f.Id == used.FrameworkId);
                var version = framework?.Versions.FirstOrDefault(v => v.Id == used.FrameworkVersionId);
                if (framework is null || version is null)
                {
                    continue;
                }

                var rows = version.Requirements
                    .Select(requirement => new RequirementCoverage(
                        requirement,
                        links.Where(l => l.RequirementId == requirement.Id && workItems.ContainsKey(l.WorkItemId))
                            .Select(l => new LinkedWorkItem(l.Id, workItems[l.WorkItemId]))
                            .OrderBy(l => l.WorkItem.Title, StringComparer.OrdinalIgnoreCase)
                            .ToList()))
                    .ToList();

                result.Add(new FrameworkCompliance(framework, version, rows));
            }

            return new ProjectCompliance(
                result.OrderBy(f => f.Framework.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                workItems.Values.ToList());
        }
    }

    public record ProjectCompliance(IReadOnlyList<FrameworkCompliance> Frameworks, IReadOnlyList<WorkItem> WorkItems)
    {
        // US-021 R3: per user story, the codes of the requirements it is linked to
        public IReadOnlyList<string> RequirementCodesOf(Guid workItemId)
            => Frameworks.SelectMany(f => f.Requirements)
                .Where(r => r.Links.Any(l => l.WorkItem.Id == workItemId))
                .Select(r => r.Requirement.Code)
                .Distinct()
                .ToList();
    }

    // One framework version the project follows, with its requirements in the document's order
    public record FrameworkCompliance(Framework Framework, FrameworkVersion Version, IReadOnlyList<RequirementCoverage> Requirements)
    {
        public int Total => Requirements.Count;
        public int Covered => Requirements.Count(r => r.IsCovered);

        // US-021 R4: rounded to whole per cent; 0 when there are no requirements
        public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Covered / Total, MidpointRounding.AwayFromZero);
    }

    public record RequirementCoverage(Requirement Requirement, IReadOnlyList<LinkedWorkItem> Links)
    {
        // US-021 R2, R5: covered when at least one active link exists
        public bool IsCovered => Links.Count > 0;
    }

    public record LinkedWorkItem(Guid LinkId, WorkItem WorkItem);
}
