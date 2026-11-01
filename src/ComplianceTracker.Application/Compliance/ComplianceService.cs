using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Compliance
{
    // Storage for links between requirements and user stories (implemented in Infrastructure)
    public interface IRequirementLinkRepository
    {
        // The project's active links
        Task<IReadOnlyList<RequirementLink>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<RequirementLink?> GetAsync(Guid projectId, Guid linkId, CancellationToken cancellationToken = default);

        Task AddAsync(RequirementLink link, CancellationToken cancellationToken = default);
    }

    // US-021: links a project's user story to one of its requirements by hand, or removes the link.
    // AI suggestions (US-020) will add links the same way after a person approves them.
    public class ComplianceService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ComplianceService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRequirementLinkRepository links,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // The user story must be the project's, and the requirement must be in a framework version the project follows.
        // A pair can only be linked once at a time.
        public async Task LinkAsync(Guid projectId, Guid requirementId, Guid workItemId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken);

            var workItem = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == workItemId)
                ?? throw new DomainException("User story not found");

            var requirement = await RequirementOfProjectAsync(organizationId, project, requirementId, cancellationToken)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");

            var active = await _links.ListActiveAsync(project.Id, cancellationToken);
            if (active.Any(l => l.RequirementId == requirementId && l.WorkItemId == workItemId))
            {
                throw new DomainException($"{workItem.Title} is already linked to {requirement.Code}");
            }

            await _links.AddAsync(RequirementLink.Create(project.Id, requirementId, workItemId, userId, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.RequirementLinked, project.Id, $"{requirement.Code} ↔ {workItem.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The link is kept with the time it was removed
        public async Task UnlinkAsync(Guid projectId, Guid linkId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            var link = await _links.GetAsync(project.Id, linkId, cancellationToken)
                ?? throw new DomainException("Link not found");

            link.Remove(userId, Now);

            var requirement = await RequirementOfProjectAsync(organizationId, project, link.RequirementId, cancellationToken);
            var workItem = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == link.WorkItemId);
            await _log.LogAsync(ActivityAction.RequirementUnlinked, project.Id,
                $"{requirement?.Code ?? "?"} ↔ {workItem?.Title ?? "?"}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The member's own project; US-024 R4: nothing changes at end of support
        private async Task<(Guid OrganizationId, Guid UserId, Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization.Id, user.Id, project);
        }

        // The requirement if it belongs to one of the framework versions the project follows now
        private async Task<Requirement?> RequirementOfProjectAsync(Guid organizationId, Project project, Guid requirementId, CancellationToken cancellationToken)
        {
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(
                organizationId, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var versionIds = project.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();

            return frameworks
                .SelectMany(f => f.Versions)
                .Where(v => versionIds.Contains(v.Id))
                .SelectMany(v => v.Requirements)
                .FirstOrDefault(r => r.Id == requirementId);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
