using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-047: "ProjectChecklistEntries", one row per project and checklist item
    public class ProjectChecklistEntryConfiguration : IEntityTypeConfiguration<ProjectChecklistEntry>
    {
        public void Configure(EntityTypeBuilder<ProjectChecklistEntry> builder)
        {
            builder.ToTable("ProjectChecklistEntries");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.ItemKey).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Comment).HasMaxLength(ProjectChecklistEntry.MaxTextLength);
            builder.Property(e => e.OffReason).HasMaxLength(ProjectChecklistEntry.MaxTextLength);
            builder.Property(e => e.EvidenceUrl).HasMaxLength(ProjectChecklistEntry.MaxUrlLength);
            builder.Property(e => e.DoneRole).HasConversion<string>().HasMaxLength(50);

            builder.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(e => new { e.ProjectId, e.ItemKey }).IsUnique();
        }
    }
}
