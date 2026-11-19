using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-191
    public class SupplierRepository : ISupplierRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public SupplierRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Supplier>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Suppliers.Include(s => s.Evidence).Include(s => s.Contacts).Where(s => s.OrganizationId == organizationId).AsSplitQuery()
                .ToListAsync(cancellationToken);

        public Task<Supplier?> GetAsync(Guid organizationId, Guid supplierId, CancellationToken cancellationToken = default)
            => _db.Suppliers.Include(s => s.Evidence).Include(s => s.Contacts).AsSplitQuery()
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Id == supplierId, cancellationToken);

        public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
            => await _db.Suppliers.AddAsync(supplier, cancellationToken);

        public async Task AddFileAsync(Guid evidenceId, byte[] content, CancellationToken cancellationToken = default)
            => await _db.SupplierEvidenceFiles.AddAsync(new SupplierEvidenceFile { EvidenceId = evidenceId, Content = content }, cancellationToken);

        public async Task<byte[]?> GetFileAsync(Guid evidenceId, CancellationToken cancellationToken = default)
            => await _db.SupplierEvidenceFiles.Where(f => f.EvidenceId == evidenceId).Select(f => f.Content).FirstOrDefaultAsync(cancellationToken);
    }
}
