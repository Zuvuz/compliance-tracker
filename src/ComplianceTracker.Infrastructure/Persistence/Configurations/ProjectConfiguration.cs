using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Project is stored in the table "Projects" (US-008)
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");

            // The id is set by the domain, not by the database
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

            // Free text without a length limit (text in PostgreSQL). Empty strings, never null.
            builder.Property(p => p.Description).IsRequired();
            builder.Property(p => p.IntendedPurpose).IsRequired();
            builder.Property(p => p.Scope).IsRequired();

            // Stored as text, like all enums
            builder.Property(p => p.Phase).HasConversion<string>().HasMaxLength(50);

            // Belongs to exactly one organisation (R5).
            // Restrict: an organisation cannot be deleted while it has projects.
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up "does this organisation already have a project with this name?"
            builder.HasIndex(p => new { p.OrganizationId, p.Name });

            // US-011: the project's frameworks, also removed ones (the history).
            // EF writes straight into the private field _frameworks; AutoInclude loads them with the project.
            builder.HasMany(p => p.FrameworkHistory)
                .WithOne()
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(p => p.FrameworkHistory)
                .HasField("_frameworks")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();

            // Frameworks is calculated from FrameworkHistory and not stored
            builder.Ignore(p => p.Frameworks);
            builder.Ignore(p => p.IsClosed);

            // US-018: the GitHub link is stored in the Projects table (columns GitHub_Repository, ...).
            // Its skipped issues are a small table of their own, replaced at every sync.
            builder.OwnsOne(p => p.GitHub, link =>
            {
                link.Property(l => l.Repository).HasMaxLength(140).IsRequired();
                link.Property(l => l.Label).HasMaxLength(100).IsRequired();
                link.Property(l => l.LastSyncError).HasMaxLength(500);
                link.Ignore(l => l.HasToken);

                // US-031: only the encrypted token is stored (Data Protection output, base64url)
                link.Property(l => l.EncryptedToken).HasMaxLength(2000);

                link.OwnsMany(l => l.Skipped, skipped =>
                {
                    skipped.ToTable("SkippedIssues");
                    skipped.Property<int>("Id");
                    skipped.HasKey("Id");
                    skipped.Property(i => i.ExternalReference).HasMaxLength(200).IsRequired();
                    skipped.Property(i => i.Title).HasMaxLength(500).IsRequired();
                    skipped.Property(i => i.Reason).HasMaxLength(200).IsRequired();
                });
                link.Navigation(l => l.Skipped).HasField("_skipped").UsePropertyAccessMode(PropertyAccessMode.Field);
            });
        }
    }
}
