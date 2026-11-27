using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-219: EF Core implementation of IPageVisitRepository. Every read is filtered by the organisation.
    public class PageVisitRepository : IPageVisitRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public PageVisitRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(PageVisit visit, CancellationToken cancellationToken = default)
            => await _db.PageVisits.AddAsync(visit, cancellationToken);

        public async Task<PageVisit?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => await _db.PageVisits.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        public async Task<IReadOnlyList<PageVisit>> SearchAsync(AccessLogSearch search, CancellationToken cancellationToken = default)
        {
            var query = _db.PageVisits.AsNoTracking().Where(v => v.OrganizationId == search.OrganizationId);
            if (search.ProjectId is { } projectId)
            {
                query = query.Where(v => v.ProjectId == projectId);
            }

            if (search.UserId is { } userId)
            {
                query = query.Where(v => v.UserId == userId);
            }

            if (search.Kind is { } kind)
            {
                query = query.Where(v => v.Kind == kind);
            }

            // A visit belongs to a period when it was open in it
            if (search.From is { } from)
            {
                query = query.Where(v => v.LastSeenAt >= from);
            }

            if (search.To is { } to)
            {
                query = query.Where(v => v.StartedAt <= to);
            }

            return await query.OrderByDescending(v => v.StartedAt).Take(search.Max).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PageVisit>> SeenSinceAsync(Guid organizationId, Guid? projectId, DateTime since, CancellationToken cancellationToken = default)
            => await _db.PageVisits.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.LastSeenAt >= since && !v.Ended && (projectId == null || v.ProjectId == projectId))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<PageVisit>> ForItemAsync(Guid organizationId, VisitKind kind, Guid itemId, CancellationToken cancellationToken = default)
            => await _db.PageVisits.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.Kind == kind && v.ItemId == itemId)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<PageVisit>> ForUserAsync(Guid organizationId, Guid userId, int max, CancellationToken cancellationToken = default)
            => await _db.PageVisits.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.UserId == userId)
                .OrderByDescending(v => v.StartedAt)
                .Take(max)
                .ToListAsync(cancellationToken);

        // R6: deleted in the database at once, not through the change tracker
        public async Task<int> DeleteStartedBeforeAsync(Guid organizationId, DateTime before, CancellationToken cancellationToken = default)
            => await _db.PageVisits.Where(v => v.OrganizationId == organizationId && v.StartedAt < before).ExecuteDeleteAsync(cancellationToken);
    }
}
