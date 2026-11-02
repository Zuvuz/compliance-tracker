using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How MappingSuggestion is stored in the table "MappingSuggestions" (US-020, US-036)
    public class MappingSuggestionConfiguration : IEntityTypeConfiguration<MappingSuggestion>
    {
        public void Configure(EntityTypeBuilder<MappingSuggestion> builder)
        {
            builder.ToTable("MappingSuggestions");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.RejectionReason).HasConversion<string>().HasMaxLength(40);
            builder.Property(s => s.Rationale).HasMaxLength(1000);
            builder.Property(s => s.Comment).HasMaxLength(1000);
            builder.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            builder.Property(s => s.RationaleModel).HasMaxLength(200);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Ignore(s => s.IsPending);

            // Restrict: nothing a suggestion points at can be deleted; suggestions are the AI's audit trail
            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(s => s.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(s => s.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ProjectId, s.WorkItemId });
            builder.HasIndex(s => new { s.Status, s.CreatedAt });
        }
    }
}
