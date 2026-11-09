using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-142 R3
    public class Nis2AssessmentRepository : INis2AssessmentRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public Nis2AssessmentRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Nis2Assessment>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Nis2Assessments
                .Where(a => a.OrganizationId == organizationId)
                .OrderByDescending(a => a.AssessedAt)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Nis2Assessment assessment, CancellationToken cancellationToken = default)
            => await _db.Nis2Assessments.AddAsync(assessment, cancellationToken);
    }
}
