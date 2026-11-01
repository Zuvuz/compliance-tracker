using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRequirementLinkRepository (US-021).
    // The organisation check happens before, on the project (ComplianceService, ComplianceQueries).
    public class RequirementLinkRepository : IRequirementLinkRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RequirementLinkRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RequirementLink>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.RequirementLinks
                .Where(l => l.ProjectId == projectId && l.RemovedAt == null)
                .ToListAsync(cancellationToken);

        public Task<RequirementLink?> GetAsync(Guid projectId, Guid linkId, CancellationToken cancellationToken = default)
            => _db.RequirementLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.ProjectId == projectId, cancellationToken);

        public async Task AddAsync(RequirementLink link, CancellationToken cancellationToken = default)
            => await _db.RequirementLinks.AddAsync(link, cancellationToken);
    }
}
