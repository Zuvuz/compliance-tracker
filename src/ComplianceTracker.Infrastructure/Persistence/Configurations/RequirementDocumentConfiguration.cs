using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How RequirementDocument is stored in "RequirementDocuments", its versions in "RequirementDocumentVersions"
    // and their files in "RequirementDocumentFiles" (US-032)
    public class RequirementDocumentConfiguration : IEntityTypeConfiguration<RequirementDocument>
    {
        public void Configure(EntityTypeBuilder<RequirementDocument> builder)
        {
            builder.ToTable("RequirementDocuments");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
            builder.Property(d => d.IdPattern).HasMaxLength(IdPattern.MaxLength); // US-033

            // Calculated, not stored
            builder.Ignore(d => d.Latest);
            builder.Ignore(d => d.NextVersionNumber);

            // Restrict: a project cannot be deleted while it has requirement specifications
            builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);

            // The versions belong to the specification; loaded with it, written straight into the private field
            builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(d => d.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

            builder.HasIndex(d => d.ProjectId);
        }
    }

    public class RequirementDocumentVersionConfiguration : IEntityTypeConfiguration<RequirementDocumentVersion>
    {
        public void Configure(EntityTypeBuilder<RequirementDocumentVersion> builder)
        {
            builder.ToTable("RequirementDocumentVersions");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();
            builder.Property(v => v.FileName).IsRequired().HasMaxLength(200);
            builder.Property(v => v.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64);

            // R3: the skipped rows, as a PostgreSQL text array
            builder.Property(v => v.SkippedRows).IsRequired();

            builder.Ignore(v => v.Summary);

            builder.HasOne<User>().WithMany().HasForeignKey(v => v.UploadedById).OnDelete(DeleteBehavior.Restrict);

            // One number per version of a specification
            builder.HasIndex(v => new { v.DocumentId, v.Number }).IsUnique();
        }
    }

    public class RequirementDocumentFileConfiguration : IEntityTypeConfiguration<RequirementDocumentFile>
    {
        public void Configure(EntityTypeBuilder<RequirementDocumentFile> builder)
        {
            builder.ToTable("RequirementDocumentFiles");
            builder.HasKey(f => f.VersionId);
            builder.Property(f => f.Content).IsRequired(); // bytea in PostgreSQL

            builder.HasOne<RequirementDocumentVersion>()
                .WithOne()
                .HasForeignKey<RequirementDocumentFile>(f => f.VersionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
