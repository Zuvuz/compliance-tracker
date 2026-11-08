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
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.AddedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.ProjectId);

            // US-136: evidence made from an organisation's document
            builder.Ignore(e => e.IsFromOrganisation);
            builder.HasOne<Domain.Organizations.OrganisationDocument>().WithMany().HasForeignKey(e => e.OrganisationDocumentId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // R1: exactly one of a link and a file
            builder.ToTable(t => t.HasCheckConstraint("CK_Evidence_LinkOrFile", "(\"Url\" IS NULL) <> (\"FileName\" IS NULL)"));
        }
    }

    // US-059: which requirements an evidence proves, in "EvidenceLinks"
    public class EvidenceLinkConfiguration : IEntityTypeConfiguration<EvidenceLink>
    {
        public void Configure(EntityTypeBuilder<EvidenceLink> builder)
        {
            builder.ToTable("EvidenceLinks");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.Source).HasConversion<string>().HasMaxLength(50);
            builder.Property(l => l.LinkedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(l => l.IsActive);

            builder.HasOne<Project>().WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Evidence>().WithMany().HasForeignKey(l => l.EvidenceId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(l => l.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.LinkedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(l => new { l.ProjectId, l.RequirementId });

            // R2: a pair is linked at most once at a time
            builder.HasIndex(l => new { l.EvidenceId, l.RequirementId }).IsUnique().HasFilter("\"RemovedAt\" IS NULL");
        }
    }

    // US-059 R3: the AI's suggestions, in "EvidenceLinkSuggestions"
    public class EvidenceLinkSuggestionConfiguration : IEntityTypeConfiguration<EvidenceLinkSuggestion>
    {
        public void Configure(EntityTypeBuilder<EvidenceLinkSuggestion> builder)
        {
            builder.ToTable("EvidenceLinkSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Rationale).HasMaxLength(1000);
            builder.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            builder.Property(s => s.RationaleModel).HasMaxLength(200);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.RejectionReason).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Comment).HasMaxLength(1000);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Evidence>().WithMany().HasForeignKey(s => s.EvidenceId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(s => s.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ProjectId, s.EvidenceId });
        }
    }

    // US-058: suggestions from tests and pull requests, in "EvidenceSuggestions"
    public class EvidenceSuggestionConfiguration : IEntityTypeConfiguration<EvidenceSuggestion>
    {
        public void Configure(EntityTypeBuilder<EvidenceSuggestion> builder)
        {
            builder.ToTable("EvidenceSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Source).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Title).IsRequired().HasMaxLength(EvidenceSuggestion.MaxTitleLength);
            builder.Property(s => s.Url).HasMaxLength(Evidence.MaxUrlLength);
            builder.Property(s => s.ExternalId).IsRequired().HasMaxLength(500);
            builder.Property(s => s.PassingScenarios).HasMaxLength(EvidenceSuggestion.MaxScenariosLength);
            builder.Property(s => s.FailingScenarios).HasMaxLength(EvidenceSuggestion.MaxScenariosLength);
            builder.Property(s => s.ResultsFileName).HasMaxLength(200);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.RejectionReason).HasMaxLength(1000);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(s => s.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Evidence>().WithMany().HasForeignKey(s => s.EvidenceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // R5: the same source is suggested once per requirement
            builder.HasIndex(s => new { s.ProjectId, s.RequirementId, s.ExternalId }).IsUnique();
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
