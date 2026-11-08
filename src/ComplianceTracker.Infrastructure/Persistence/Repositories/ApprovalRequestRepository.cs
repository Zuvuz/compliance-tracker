using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-135 repository. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class ApprovalRequestRepository : IApprovalRequestRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ApprovalRequestRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<ApprovalRequest?> GetAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken = default)
            => _db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.OrganizationId == organizationId, cancellationToken);

        public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.ApprovalRequests
                .Where(r => r.OrganizationId == organizationId && r.Status == ApprovalStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ApprovalRequest>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ApprovalRequests
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
            => await _db.ApprovalRequests.AddAsync(request, cancellationToken);
    }
}
