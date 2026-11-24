using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-214
    public class AssetRepository : IAssetRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public AssetRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<ClassificationScheme?> GetSchemeAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => _db.ClassificationSchemes.Local.FirstOrDefault(s => s.OrganizationId == organizationId)
               ?? await _db.ClassificationSchemes.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

        public async Task AddSchemeAsync(ClassificationScheme scheme, CancellationToken cancellationToken = default)
            => await _db.ClassificationSchemes.AddAsync(scheme, cancellationToken);

        public async Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Assets.Where(a => a.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
            => await _db.Assets.AddAsync(asset, cancellationToken);
    }
}
