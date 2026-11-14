using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of INeedsActionReassessmentRepository (US-156). The organisation is stored on each request.
    public class NeedsActionReassessmentRepository : INeedsActionReassessmentRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public NeedsActionReassessmentRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<NeedsActionReassessment?> OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
            => _db.NeedsActionReassessments.Local.FirstOrDefault(r => r.ProjectId == projectId && r.DoneAt == null)
               ?? await _db.NeedsActionReassessments.FirstOrDefaultAsync(r => r.ProjectId == projectId && r.DoneAt == null, cancellationToken);

        public Task<NeedsActionReassessment?> LatestAsync(Guid projectId, CancellationToken cancellationToken = default)
            => _db.NeedsActionReassessments.Where(r => r.ProjectId == projectId).OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<NeedsActionReassessment>> DueAsync(DateTime now, int max, CancellationToken cancellationToken = default)
            => await _db.NeedsActionReassessments.Where(r => r.DoneAt == null && r.DueAt <= now).OrderBy(r => r.DueAt).Take(max).ToListAsync(cancellationToken);

        public async Task AddAsync(NeedsActionReassessment reassessment, CancellationToken cancellationToken = default)
            => await _db.NeedsActionReassessments.AddAsync(reassessment, cancellationToken);
    }
}
