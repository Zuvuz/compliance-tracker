using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // EF Core's view of the PostgreSQL database. Each DbSet is a table (Users are reached through Organizations).
    // How each class is stored is described in the Configurations folder.
    public class ComplianceTrackerDbContext : DbContext
    {
        public ComplianceTrackerDbContext(DbContextOptions<ComplianceTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<ActivityLogEntry> ActivityLog => Set<ActivityLogEntry>();
        public DbSet<Framework> Frameworks => Set <Framework>();
        public DbSet<FrameworkVersion> FrameworkVersions => Set<FrameworkVersion>();
        public DbSet<CatalogDocument> CatalogDocuments => Set<CatalogDocument>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<OrganizationFramework> OrganizationFrameworks => Set<OrganizationFramework>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Picks up every IEntityTypeConfiguration class in the Configurations folder
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplianceTrackerDbContext).Assembly);
        }
    }
}
