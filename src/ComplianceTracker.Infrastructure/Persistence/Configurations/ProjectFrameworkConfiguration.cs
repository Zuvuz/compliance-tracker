using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How ProjectFramework (a framework version a project follows) is stored in the table "ProjectFrameworks" (US-011).
    // Rows are never deleted, so the history is kept. The link to Project is set up in ProjectConfiguration.
    public class ProjectFrameworkConfiguration : IEntityTypeConfiguration<ProjectFramework>
    {
        public void Configure(EntityTypeBuilder<ProjectFramework> builder)
        {
            builder.ToTable("ProjectFrameworks");

            // The id is set by the domain, not by the database
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();

            // IsActive is calculated from RemovedAt and not stored
            builder.Ignore(f => f.IsActive);

            // Restrict everywhere: nothing a row points at can be deleted while the history exists
            builder.HasOne<Framework>()
                .WithMany()
                .HasForeignKey(f => f.FrameworkId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<FrameworkVersion>()
                .WithMany()
                .HasForeignKey(f => f.FrameworkVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(f => f.AddedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(f => f.RemovedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // R3 in the database too: at most one active row per project and framework
            builder.HasIndex(f => new { f.ProjectId, f.FrameworkId })
                .IsUnique()
                .HasFilter("\"RemovedAt\" IS NULL");

            // Speeds up "which projects use this version?" (organisation overview, R5)
            builder.HasIndex(f => f.FrameworkVersionId);
        }
    }
}
