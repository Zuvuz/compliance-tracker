using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Users;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How FrameworkVersion is stored in the table "FrameworkVersions" (the PDF itself is in CatalogDocuments)
    public class FrameworkVersionConfiguration : IEntityTypeConfiguration<FrameworkVersion>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<FrameworkVersion> builder)
        {
            builder.ToTable("FrameworkVersions");

            // US-010: the role when the decision was made; never changed afterwards
            builder.Property(v => v.ActivatedRole).HasConversion<string>().HasMaxLength(50);

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

            // US-070: the extraction check as JSON; null for versions without one
            builder.Property(v => v.Extraction)
                .HasColumnType("jsonb")
                .HasConversion(
                    check => check == null ? null : JsonSerializer.Serialize(check, Json),
                    json => json == null ? null : JsonSerializer.Deserialize<ExtractionCheck>(json, Json),
                    new ValueComparer<ExtractionCheck?>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => c == null ? 0 : JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c));

            // US-137: the completeness check as JSON; null for versions without one
            builder.Property(v => v.Completeness)
                .HasColumnType("jsonb")
                .HasConversion(
                    check => check == null ? null : JsonSerializer.Serialize(check, Json),
                    json => json == null ? null : JsonSerializer.Deserialize<CompletenessCheck>(json, Json),
                    new ValueComparer<CompletenessCheck?>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => c == null ? 0 : JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c));
            builder.Ignore(v => v.NeedsApprovalToActivate);
            builder.Ignore(v => v.RequirementsStamp);

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
                BuiltInVersion(BuiltInFrameworks.Cra.Version2Id, BuiltInFrameworks.Cra.FrameworkId,
                    BuiltInFrameworks.Cra.PublishedAt, BuiltInFrameworks.Cra.SourceReference, BuiltInFrameworks.Cra.Version2AppliesFrom, number: 2),
                BuiltInVersion(BuiltInFrameworks.AiAct.VersionId, BuiltInFrameworks.AiAct.FrameworkId,
                    BuiltInFrameworks.AiAct.PublishedAt, BuiltInFrameworks.AiAct.SourceReference, BuiltInFrameworks.AiAct.AppliesFrom),
                // US-142
                BuiltInVersion(BuiltInFrameworks.Nis2.VersionId, BuiltInFrameworks.Nis2.FrameworkId,
                    BuiltInFrameworks.Nis2.PublishedAt, BuiltInFrameworks.Nis2.SourceReference, BuiltInFrameworks.Nis2.AppliesFrom),
                BuiltInVersion(BuiltInFrameworks.Nis2Denmark.VersionId, BuiltInFrameworks.Nis2Denmark.FrameworkId,
                    BuiltInFrameworks.Nis2Denmark.PublishedAt, BuiltInFrameworks.Nis2Denmark.SourceReference, BuiltInFrameworks.Nis2Denmark.AppliesFrom),
                BuiltInVersion(BuiltInFrameworks.Nis2Technical.VersionId, BuiltInFrameworks.Nis2Technical.FrameworkId,
                    BuiltInFrameworks.Nis2Technical.PublishedAt, BuiltInFrameworks.Nis2Technical.SourceReference, BuiltInFrameworks.Nis2Technical.AppliesFrom));
        }

        // Seed data for a built-in version: no file, no uploader, a link to the official text
        private static object BuiltInVersion(Guid id, Guid frameworkId, DateTime publishedAt, string sourceReference, DateOnly appliesFrom, int number = 1)
            => new
            {
                Id = id,
                FrameworkId = frameworkId,
                Number = number,
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
