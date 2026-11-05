using ComplianceTracker.Domain.Users;
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

    // Storage for "not applicable" decisions (US-043)
    public interface IRequirementApplicabilityRepository
    {
        // The project's requirements that are not applicable now
        Task<IReadOnlyList<RequirementApplicability>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(RequirementApplicability applicability, CancellationToken cancellationToken = default);
    }

    // Storage for evidence (US-057). A file's content is stored apart, so listing never loads the files.
    public interface IEvidenceRepository
    {
        // The project's evidence that is not removed
        Task<IReadOnlyList<Evidence>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<Evidence?> GetAsync(Guid projectId, Guid evidenceId, CancellationToken cancellationToken = default);

        // content: the file's bytes, or null for a link
        Task AddAsync(Evidence evidence, byte[]? content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid evidenceId, CancellationToken cancellationToken = default);

        // US-059: which requirements each evidence proves. Only links that are not removed.
        Task<IReadOnlyList<EvidenceLink>> ListActiveLinksAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddLinkAsync(EvidenceLink link, CancellationToken cancellationToken = default);
    }

    // US-021: links a project's user story to one of its requirements by hand, or removes the link.
    // US-043: marks a requirement as not applicable to the project, with a reason, or applicable again.
    // US-057: adds evidence (a link or a file) to a requirement, or removes it.
    // AI suggestions (US-020) will add links the same way after a person approves them.
    public class ComplianceService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IEvidenceRepository _evidence;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ComplianceService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IEvidenceRepository evidence,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _applicability = applicability;
            _evidence = evidence;
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

            var role = (await _member.RequireAsync(cancellationToken)).User.Role;
            await _links.AddAsync(RequirementLink.Create(project.Id, requirementId, workItemId, userId, Now, LinkSource.Manual, role), cancellationToken);
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

        // US-043 R5: the requirement does not apply to the project; the reason is required
        public async Task MarkNotApplicableAsync(Guid projectId, Guid requirementId, string reason, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken, Permission.DecideCompliance);
            var requirement = await RequirementOfProjectAsync(organizationId, project, requirementId, cancellationToken)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");

            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirementId))
            {
                throw new DomainException($"{requirement.Code} is already marked as not applicable");
            }

            var decision = RequirementApplicability.NotApplicable(project.Id, requirementId, reason, userId, Now,
                (await _member.RequireAsync(cancellationToken)).User.Role);
            await _applicability.AddAsync(decision, cancellationToken);
            await _log.LogAsync(ActivityAction.RequirementMarkedNotApplicable, project.Id, $"{requirement.Code}: {decision.Reason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-043 R5: the requirement applies again; the decision is kept with the time it was revoked
        public async Task MarkApplicableAsync(Guid projectId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken, Permission.DecideCompliance);
            var decision = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).FirstOrDefault(a => a.RequirementId == requirementId)
                ?? throw new DomainException("The requirement already applies");

            decision.Revoke(userId, Now);
            var requirement = await RequirementOfProjectAsync(organizationId, project, requirementId, cancellationToken);
            await _log.LogAsync(ActivityAction.RequirementMarkedApplicable, project.Id, requirement?.Code ?? "?", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-057 R1, R6: a link as evidence for one of the project's applicable requirements
        public Task AddEvidenceLinkAsync(
            Guid projectId, Guid requirementId, string title, string url, DateOnly? validUntil, CancellationToken cancellationToken = default)
            => AddEvidenceAsync(projectId, requirementId,
                (userId, role, at) => (Evidence.ForLink(projectId, title, url, validUntil, userId, role, at), null),
                cancellationToken);

        // US-057 R2: a file as evidence; EvidenceFile checks the size, type and name
        public Task AddEvidenceFileAsync(
            Guid projectId, Guid requirementId, string title, string fileName, byte[] content, DateOnly? validUntil,
            CancellationToken cancellationToken = default)
            => AddEvidenceAsync(projectId, requirementId,
                (userId, role, at) =>
                {
                    var file = EvidenceFile.Create(fileName, content);
                    return (Evidence.ForFile(projectId, title, file, validUntil, userId, role, at), file.Content);
                },
                cancellationToken);

        // US-059: the cross on a Compliance tab row takes the evidence off that requirement (history kept). When it proved
        // nothing else, the evidence itself is removed too, as in US-057 R5; otherwise it stays on the Evidence tab.
        public async Task RemoveEvidenceAsync(Guid projectId, Guid evidenceId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            var evidence = await _evidence.GetAsync(project.Id, evidenceId, cancellationToken)
                ?? throw new DomainException("Evidence not found");
            var links = (await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).Where(l => l.EvidenceId == evidence.Id).ToList();
            var link = links.FirstOrDefault(l => l.RequirementId == requirementId)
                ?? throw new DomainException("The evidence is not linked to the requirement");

            link.Remove(userId, Now);
            var requirement = await RequirementOfProjectAsync(organizationId, project, requirementId, cancellationToken);
            if (links.Count == 1)
            {
                evidence.Remove(userId, Now);
                await _log.LogAsync(ActivityAction.EvidenceRemoved, project.Id, $"{requirement?.Code ?? "?"}: {evidence.Title}", cancellationToken);
            }
            else
            {
                await _log.LogAsync(ActivityAction.EvidenceUnlinked, project.Id, $"{requirement?.Code ?? "?"}: {evidence.Title}", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-059: "Pick existing evidence" on a Compliance tab row
        public async Task LinkExistingEvidenceAsync(Guid projectId, Guid requirementId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            var requirement = await ApplicableRequirementAsync(organizationId, project, requirementId, cancellationToken);
            var evidence = await _evidence.GetAsync(project.Id, evidenceId, cancellationToken)
                ?? throw new DomainException("Evidence not found");
            if ((await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).Any(l => l.EvidenceId == evidence.Id && l.RequirementId == requirement.Id))
            {
                throw new DomainException($"{evidence.Title} is already linked to {requirement.Code}");
            }

            var role = (await _member.RequireAsync(cancellationToken)).User.Role;
            await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, userId, role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceLinked, project.Id, $"{requirement.Code}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task AddEvidenceAsync(
            Guid projectId, Guid requirementId, Func<Guid, UserRole?, DateTime, (Evidence Evidence, byte[]? Content)> create,
            CancellationToken cancellationToken)
        {
            var (organizationId, userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            var requirement = await ApplicableRequirementAsync(organizationId, project, requirementId, cancellationToken);

            var role = (await _member.RequireAsync(cancellationToken)).User.Role;
            var (evidence, content) = create(userId, role, Now);
            await _evidence.AddAsync(evidence, content, cancellationToken);
            await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, userId, role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceAdded, project.Id, $"{requirement.Code}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // A requirement of a version the project follows, which applies to it (US-043, US-059 R5)
        private async Task<Requirement> ApplicableRequirementAsync(Guid organizationId, Project project, Guid requirementId, CancellationToken cancellationToken)
        {
            var requirement = await RequirementOfProjectAsync(organizationId, project, requirementId, cancellationToken)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");

            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirementId))
            {
                throw new DomainException($"{requirement.Code} does not apply to the project");
            }

            return requirement;
        }

        // The member's own project; US-024 R4: nothing changes at end of support
        private async Task<(Guid OrganizationId, Guid UserId, Project Project)> OpenProjectAsync(
            Guid projectId, CancellationToken cancellationToken, Permission permission = Permission.LinkWork)
        {
            var (organization, user) = await _member.RequireAsync(permission, cancellationToken);
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
