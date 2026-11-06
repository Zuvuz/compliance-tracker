using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How ApplicabilitySuggestion is stored in the table "ApplicabilitySuggestions" (US-060). Kept, like the other
    // suggestions, as the AI's audit trail.
    public class ApplicabilitySuggestionConfiguration : IEntityTypeConfiguration<ApplicabilitySuggestion>
    {
        public void Configure(EntityTypeBuilder<ApplicabilitySuggestion> builder)
        {
            builder.ToTable("ApplicabilitySuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Reason).IsRequired().HasMaxLength(ApplicabilitySuggestion.MaxReasonLength);
            builder.Property(s => s.ContextUsed).IsRequired().HasMaxLength(1000);
            builder.Property(s => s.Model).IsRequired().HasMaxLength(200);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Framework>().WithMany().HasForeignKey(s => s.FrameworkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(s => s.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ProjectId, s.Status });
        }
    }
}
