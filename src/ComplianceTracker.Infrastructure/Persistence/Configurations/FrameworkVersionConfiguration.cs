using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How FrameworkVersion is stored in the table "FrameworkVersions" (the PDF itself is in CatalogDocuments)
    public class FrameworkVersionConfiguration : IEntityTypeConfiguration<FrameworkVersion>
    {
        public void Configure(EntityTypeBuilder<FrameworkVersion> builder)
        {
            builder.ToTable("FrameworkVersions");

            // The id is set by the domain, not by the database
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Property(v => v.FileName).IsRequired().HasMaxLength(255);

            // A SHA-256 hash written as hex is 64 characters; empty for built-in frameworks
            builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64);

            // Where the official text is published (built-in frameworks only)
            builder.Property(v => v.SourceReference).HasMaxLength(500);

            // US-007: Draft or Active, stored as text
            builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(v => v.IsActive);

            // Who activated it; null for drafts and built-in versions
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.ActivatedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // The requirements belong to the version. EF writes straight into the private field _requirements.
            // Not AutoInclude: they are loaded only where needed (IFrameworkRepository.GetByVersionWithRequirementsAsync).
            builder.HasMany(v => v.Requirements)
                .WithOne()
                .HasForeignKey(r => r.FrameworkVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(v => v.Requirements)
                .HasField("_requirements")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            // The version number is unique within a framework (1, 2, 3 ...)
            builder.HasIndex(v => new { v.FrameworkId, v.Number }).IsUnique();

            // Who uploaded it; null for built-in frameworks.
            // Restrict: a user cannot be deleted while they have uploaded versions.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UploadedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // The one version of each built-in framework (US-006)
            builder.HasData(
                BuiltInVersion(BuiltInFrameworks.Cra.VersionId, BuiltInFrameworks.Cra.FrameworkId,
                    BuiltInFrameworks.Cra.PublishedAt, BuiltInFrameworks.Cra.SourceReference, BuiltInFrameworks.Cra.AppliesFrom),
                BuiltInVersion(BuiltInFrameworks.AiAct.VersionId, BuiltInFrameworks.AiAct.FrameworkId,
                    BuiltInFrameworks.AiAct.PublishedAt, BuiltInFrameworks.AiAct.SourceReference, BuiltInFrameworks.AiAct.AppliesFrom));
        }

        // Seed data for a built-in version: no file, no uploader, a link to the official text
        private static object BuiltInVersion(Guid id, Guid frameworkId, DateTime publishedAt, string sourceReference, DateOnly appliesFrom)
            => new
            {
                Id = id,
                FrameworkId = frameworkId,
                Number = 1,
                FileName = "",
                SizeInBytes = 0L,
                Sha256 = "",
                UploadedById = (Guid?)null,
                UploadedAt = publishedAt,
                SourceReference = sourceReference,
                // Built-in versions follow the published regulation and are always active (US-007)
                Status = FrameworkVersionStatus.Active,
                ActivatedAt = (DateTime?)publishedAt,
                ActivatedById = (Guid?)null,
                AppliesFrom = (DateOnly?)appliesFrom
            };
    }
}
