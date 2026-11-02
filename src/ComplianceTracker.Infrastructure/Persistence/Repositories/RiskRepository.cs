using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRiskRepository (US-042).
    // The organisation check happens before, on the project (RiskService, RiskQueries).
    public class RiskRepository : IRiskRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RiskRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Risk>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.Risks.Where(r => r.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<Risk?> GetAsync(Guid projectId, Guid riskId, CancellationToken cancellationToken = default)
            => _db.Risks.FirstOrDefaultAsync(r => r.Id == riskId && r.ProjectId == projectId, cancellationToken);

        public Task<Risk?> GetByIdAsync(Guid riskId, CancellationToken cancellationToken = default)
            => _db.Risks.FirstOrDefaultAsync(r => r.Id == riskId, cancellationToken);

        public async Task AddAsync(Risk risk, CancellationToken cancellationToken = default)
            => await _db.Risks.AddAsync(risk, cancellationToken);
    }
}
