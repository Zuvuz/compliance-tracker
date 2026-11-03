using ComplianceTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ComplianceTracker.Web.Security
{
    // US-017: /health answers "Healthy" only when the app can reach its database, so Docker and CI know when it is ready.
    // The answer has no details (no connection string, no error text).
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ComplianceTrackerDbContext _db;

        public DatabaseHealthCheck(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
    }
}
