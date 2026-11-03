using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Evidence is stored in the table "Evidence", and its files in "EvidenceDocuments" (US-057)
    public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
    {
        public void Configure(EntityTypeBuilder<Evidence> builder)
        {
            builder.ToTable("Evidence");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Title).IsRequired().HasMaxLength(Evidence.MaxTitleLength);
            builder.Property(e => e.Url).HasMaxLength(Evidence.MaxUrlLength);
            builder.Property(e => e.FileName).HasMaxLength(200);
            builder.Property(e => e.ContentType).HasMaxLength(100);
            builder.Property(e => e.Sha256).HasMaxLength(64);
            builder.Property(e => e.AddedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(e => e.IsActive);
            builder.Ignore(e => e.IsFile);

            // Restrict everywhere: nothing evidence points at can be deleted while it exists
            builder.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(e => e.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.AddedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.ProjectId, e.RequirementId });

            // R1: exactly one of a link and a file
            builder.ToTable(t => t.HasCheckConstraint("CK_Evidence_LinkOrFile", "(\"Url\" IS NULL) <> (\"FileName\" IS NULL)"));
        }
    }

    public class EvidenceDocumentConfiguration : IEntityTypeConfiguration<EvidenceDocument>
    {
        public void Configure(EntityTypeBuilder<EvidenceDocument> builder)
        {
            builder.ToTable("EvidenceDocuments");
            builder.HasKey(d => d.EvidenceId);
            builder.Property(d => d.Content).IsRequired(); // bytea in PostgreSQL

            builder.HasOne<Evidence>()
                .WithOne()
                .HasForeignKey<EvidenceDocument>(d => d.EvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
