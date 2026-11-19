using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IDefinitionOfDoneRepository (US-109). The organisation check happens before, on the project.
    public class DefinitionOfDoneRepository : IDefinitionOfDoneRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public DefinitionOfDoneRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<DefinitionOfDone?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
            => _db.DefinitionsOfDone.FirstOrDefaultAsync(d => d.ProjectId == projectId, cancellationToken);

        public async Task AddAsync(DefinitionOfDone definition, CancellationToken cancellationToken = default)
            => await _db.DefinitionsOfDone.AddAsync(definition, cancellationToken);

        public async Task<IReadOnlyList<DoneTick>> ListTicksAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.DoneTicks.Where(t => t.ProjectId == projectId).ToListAsync(cancellationToken);

        public async Task AddTickAsync(DoneTick tick, CancellationToken cancellationToken = default)
            => await _db.DoneTicks.AddAsync(tick, cancellationToken);
    }
}
