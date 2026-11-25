using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-211
    public class AuditProgrammeRepository : IAuditProgrammeRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public AuditProgrammeRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<AuditProgramme?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => _db.AuditProgrammes.Include(p => p.Audits).FirstOrDefaultAsync(p => p.OrganizationId == organizationId, cancellationToken);

        public async Task AddAsync(AuditProgramme programme, CancellationToken cancellationToken = default)
            => await _db.AuditProgrammes.AddAsync(programme, cancellationToken);
    }
}
