using ComplianceTracker.Domain.Risks;
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
        public DbSet<RequirementDocument> RequirementDocuments => Set<RequirementDocument>();
        public DbSet<RequirementDocumentFile> RequirementDocumentFiles => Set<RequirementDocumentFile>();
        public DbSet<RequirementLink> RequirementLinks => Set<RequirementLink>();
        public DbSet<RequirementApplicability> RequirementApplicability => Set<RequirementApplicability>();
        public DbSet<Evidence> Evidence => Set<Evidence>();
        public DbSet<EvidenceDocument> EvidenceDocuments => Set<EvidenceDocument>();
        public DbSet<Domain.Feedback.Feedback> Feedback => Set<Domain.Feedback.Feedback>(); // US-063
        public DbSet<Configurations.FeedbackScreenshot> FeedbackScreenshots => Set<Configurations.FeedbackScreenshot>();
        public DbSet<EvidenceLink> EvidenceLinks => Set<EvidenceLink>(); // US-059
        public DbSet<EvidenceLinkSuggestion> EvidenceLinkSuggestions => Set<EvidenceLinkSuggestion>();
        public DbSet<EvidenceSuggestion> EvidenceSuggestions => Set<EvidenceSuggestion>(); // US-058
        public DbSet<MappingSuggestion> MappingSuggestions => Set<MappingSuggestion>();
        public DbSet<ApplicabilitySuggestion> ApplicabilitySuggestions => Set<ApplicabilitySuggestion>(); // US-060
        public DbSet<Risk> Risks => Set<Risk>();
        public DbSet<ProjectClassification> ProjectClassifications => Set<ProjectClassification>(); // US-026
        public DbSet<Domain.Security.SecurityEvent> SecurityEvents => Set<Domain.Security.SecurityEvent>(); // US-073
        public DbSet<Invitation> Invitations => Set<Invitation>();
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
