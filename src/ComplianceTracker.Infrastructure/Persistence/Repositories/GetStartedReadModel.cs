using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-096: whether each first step has been done, as a few EXISTS queries instead of loading the data
    public class GetStartedReadModel : IGetStartedReadModel
    {
        private readonly ComplianceTrackerDbContext _db;

        public GetStartedReadModel(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<GetStartedStatus> StatusAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            var projects = _db.Projects.Where(p => p.OrganizationId == organizationId).Select(p => p.Id);

            // A built-in regulation switched on, or one of the organisation's own frameworks activated
            var regulationOn = await _db.OrganizationFrameworks.AnyAsync(f => f.OrganizationId == organizationId && f.DisabledAt == null, cancellationToken)
                || await _db.FrameworkVersions.AnyAsync(v => v.Status == FrameworkVersionStatus.Active
                    && _db.Frameworks.Any(f => f.Id == v.FrameworkId && f.OwnerOrganizationId == organizationId), cancellationToken);

            var firstProject = await _db.Projects
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.Name)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return new GetStartedStatus(
                regulationOn,
                firstProject,
                await _db.ProjectFrameworks.AnyAsync(f => projects.Contains(f.ProjectId), cancellationToken),
                await _db.WorkItems.AnyAsync(w => projects.Contains(w.ProjectId), cancellationToken),
                await _db.RequirementLinks.AnyAsync(l => projects.Contains(l.ProjectId) && l.RemovedAt == null, cancellationToken),
                await _db.Evidence.AnyAsync(e => projects.Contains(e.ProjectId) && e.RemovedAt == null, cancellationToken));
        }
    }
}
