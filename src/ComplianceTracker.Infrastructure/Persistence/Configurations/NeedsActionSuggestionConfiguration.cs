using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How NeedsActionSuggestion is stored in the table "NeedsActionSuggestions" (US-155). Kept, like the other
    // suggestions, as the AI's audit trail.
    public class NeedsActionSuggestionConfiguration : IEntityTypeConfiguration<NeedsActionSuggestion>
    {
        public void Configure(EntityTypeBuilder<NeedsActionSuggestion> builder)
        {
            builder.ToTable("NeedsActionSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Reason).IsRequired().HasMaxLength(NeedsActionSuggestion.MaxReasonLength);
            builder.Property(s => s.ContextUsed).IsRequired().HasMaxLength(1000);
            builder.Property(s => s.Model).IsRequired().HasMaxLength(200);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(s => s.IsPending);
            builder.Ignore(s => s.IsOpen);
            builder.Property(s => s.Priority).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.DecisionReason).HasMaxLength(NeedsActionSuggestion.MaxReasonLength);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Framework>().WithMany().HasForeignKey(s => s.FrameworkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(s => s.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ProjectId, s.Status });
        }
    }
}
