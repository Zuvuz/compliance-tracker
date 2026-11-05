using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of ISecurityEventRepository (US-073). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class SecurityEventRepository : ISecurityEventRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public SecurityEventRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SecurityEvent>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.SecurityEvents.Where(e => e.ProjectId == projectId).OrderByDescending(e => e.AwareAt).ToListAsync(cancellationToken);

        public Task<SecurityEvent?> GetAsync(Guid projectId, Guid eventId, CancellationToken cancellationToken = default)
            => _db.SecurityEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.ProjectId == projectId, cancellationToken);

        public async Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default)
            => await _db.SecurityEvents.AddAsync(securityEvent, cancellationToken);
    }
}
