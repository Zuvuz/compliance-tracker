using ComplianceTracker.Domain.Mappings;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // EF Core's view of the PostgreSQL database. Each DbSet is a table (Users are reached through Organizations).
    // How each class is stored is described in the Configurations folder.
    // IDataProtectionKeyContext: the Data Protection keys live here too (US-031), so encrypted tokens
    // and sign-in cookies stay readable after a restart or in a new container
    public class ComplianceTrackerDbContext : DbContext, IDataProtectionKeyContext
    {
        public ComplianceTrackerDbContext(DbContextOptions<ComplianceTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<ActivityLogEntry> ActivityLog => Set<ActivityLogEntry>();
        public DbSet<Framework> Frameworks => Set <Framework>();
        public DbSet<FrameworkVersion> FrameworkVersions => Set<FrameworkVersion>();
        public DbSet<Requirement> Requirements => Set<Requirement>();
        public DbSet<CatalogDocument> CatalogDocuments => Set<CatalogDocument>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<WorkItem> WorkItems => Set<WorkItem>();
        public DbSet<RequirementLink> RequirementLinks => Set<RequirementLink>();
        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
        public DbSet<OrganizationFramework> OrganizationFrameworks => Set<OrganizationFramework>();
        public DbSet<ProjectFramework> ProjectFrameworks => Set<ProjectFramework>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Picks up every IEntityTypeConfiguration class in the Configurations folder
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplianceTrackerDbContext).Assembly);
        }
    }
}
