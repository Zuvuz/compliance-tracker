using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-038: the AI's checks of evidence, with the person's decision, in "EvidenceChecks"
    public class EvidenceCheckConfiguration : IEntityTypeConfiguration<EvidenceCheck>
    {
        public void Configure(EntityTypeBuilder<EvidenceCheck> builder)
        {
            builder.ToTable("EvidenceChecks");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.AiVerdict).HasConversion<string>().HasMaxLength(20);
            builder.Property(c => c.DecidedVerdict).HasConversion<string>().HasMaxLength(20);
            builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(c => c.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(c => c.Missing).IsRequired().HasMaxLength(EvidenceCheck.MaxTextLength);
            builder.Property(c => c.Quotes).IsRequired().HasMaxLength(EvidenceCheck.MaxTextLength);
            builder.Property(c => c.DecisionReason).HasMaxLength(EvidenceCheck.MaxReasonLength);
            builder.Property(c => c.EvidenceFingerprint).IsRequired().HasMaxLength(64);
            builder.Property(c => c.Model).IsRequired().HasMaxLength(200);
            builder.Property(c => c.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Ignore(c => c.IsDecided);
            builder.Ignore(c => c.RefutesEvidence);
            builder.HasOne<Project>().WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Evidence>().WithMany().HasForeignKey(c => c.EvidenceId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(c => c.CheckedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(c => c.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(c => c.ProjectId);
        }
    }
}
