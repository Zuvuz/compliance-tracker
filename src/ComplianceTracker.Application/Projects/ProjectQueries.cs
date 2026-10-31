using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // What the pages read about projects (US-029 R7).
    // Only the signed-in user's organisation's projects (US-008 R5); others look like they do not exist.
    public class ProjectQueries
    {
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;

        public ProjectQueries(IProjectRepository projects, CurrentMember member)
        {
            _projects = projects;
            _member = member;
        }

        // The organisation's projects in alphabetical order (empty without an organisation)
        public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _projects.ListAsync(organization.Id, cancellationToken)
                : [];

        // One of the organisation's projects, or null
        public async Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                : null;
    }
}
