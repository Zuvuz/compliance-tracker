using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-193: "ChecklistSuggestions"
    public class ChecklistSuggestionConfiguration : IEntityTypeConfiguration<ChecklistSuggestion>
    {
        public void Configure(EntityTypeBuilder<ChecklistSuggestion> builder)
        {
            builder.ToTable("ChecklistSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.RequirementCode).IsRequired().HasMaxLength(OrganisationChecklistItem.MaxReferenceLength);
            builder.Property(s => s.Text).IsRequired().HasMaxLength(OrganisationChecklistItem.MaxTextLength);
            builder.Property(s => s.Phase).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Framework>().WithMany().HasForeignKey(s => s.FrameworkId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(s => new { s.OrganizationId, s.FrameworkId, s.RequirementCode });
        }
    }
}
