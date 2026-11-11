using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-106: the AI's story drafts, in "StoryDrafts"
    public class StoryDraftConfiguration : IEntityTypeConfiguration<StoryDraft>
    {
        public void Configure(EntityTypeBuilder<StoryDraft> builder)
        {
            builder.ToTable("StoryDrafts");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Title).IsRequired().HasMaxLength(StoryDraft.MaxTitleLength);
            builder.Property(d => d.Role).IsRequired().HasMaxLength(200);
            builder.Property(d => d.Story).IsRequired().HasMaxLength(StoryDraft.MaxTextLength);
            builder.Property(d => d.Rules).IsRequired().HasMaxLength(StoryDraft.MaxTextLength);
            builder.Property(d => d.Examples).IsRequired().HasMaxLength(StoryDraft.MaxTextLength);
            builder.Property(d => d.Model).IsRequired().HasMaxLength(200);
            builder.Property(d => d.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(d => d.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(d => d.DiscardReason).HasMaxLength(1000);
            builder.Ignore(d => d.IsOpen);

            builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(d => d.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(d => d.ProjectId);
        }
    }

    // US-106 R12: the AI's measure suggestions for risks, in "RiskMeasureSuggestions"
    public class RiskMeasureSuggestionConfiguration : IEntityTypeConfiguration<RiskMeasureSuggestion>
    {
        public void Configure(EntityTypeBuilder<RiskMeasureSuggestion> builder)
        {
            builder.ToTable("RiskMeasureSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.RejectionReason).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Comment).HasMaxLength(1000);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Risk>().WithMany().HasForeignKey(s => s.RiskId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(s => new { s.ProjectId, s.RiskId });
        }
    }
}
