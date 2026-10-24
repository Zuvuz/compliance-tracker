using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence
{
    public class ComplianceTrackerDbContext : DbContext
    {
        public ComplianceTrackerDbContext(DbContextOptions<ComplianceTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<ActivityLogEntry> ActivityLog => Set<ActivityLogEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Henter alle IEntityTypeConfiguration-klasser i mappen Configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplianceTrackerDbContext).Assembly);
        }
    }
}
