using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // What the pages read about work items (US-018). Only the signed-in user's own projects (R9).
    public class WorkItemQueries
    {
        private readonly IProjectRepository _projects;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementDocumentRepository _documents;
        private readonly CurrentMember _member;

        public WorkItemQueries(IProjectRepository projects, IWorkItemRepository workItems, IRequirementDocumentRepository documents, CurrentMember member)
        {
            _projects = projects;
            _workItems = workItems;
            _documents = documents;
            _member = member;
        }

        // The project's work items, or null if the project is not the user's organisation's
        public async Task<IReadOnlyList<WorkItem>?> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is null)
            {
                return null;
            }

            return await _workItems.ListAsync(projectId, cancellationToken);
        }

        // US-032: the project's requirement specifications with their versions, or none if the project is not the user's
        public async Task<IReadOnlyList<RequirementDocument>> DocumentsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await CanSeeAsync(projectId, cancellationToken)
                ? await _documents.ListAsync(projectId, cancellationToken)
                : [];

        // US-032 R7, R8: one version's file, for anyone in the organisation; null for other organisations
        public async Task<(RequirementDocumentVersion Version, byte[] Content)?> DocumentFileAsync(
            Guid projectId, Guid versionId, CancellationToken cancellationToken = default)
        {
            var version = (await DocumentsAsync(projectId, cancellationToken))
                .SelectMany(d => d.Versions)
                .FirstOrDefault(v => v.Id == versionId);
            if (version is null || await _documents.GetContentAsync(version.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (version, content);
        }

        // R9: whether the user may follow the project's work items, e.g. on the live page (SignalR)
        public async Task<bool> CanSeeAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
               && await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not null;
    }
}
