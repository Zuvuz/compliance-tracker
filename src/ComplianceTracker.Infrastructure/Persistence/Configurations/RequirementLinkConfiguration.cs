using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How RequirementLink is stored in the table "RequirementLinks" (US-021)
    public class RequirementLinkConfiguration : IEntityTypeConfiguration<RequirementLink>
    {
        public void Configure(EntityTypeBuilder<RequirementLink> builder)
        {
            builder.ToTable("RequirementLinks");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.Source).HasConversion<string>().HasMaxLength(30);
            builder.Ignore(l => l.IsActive);

            // Restrict everywhere: nothing a link points at can be deleted while the link exists
            builder.HasOne<Project>().WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(l => l.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(l => l.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.LinkedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // A pair is linked at most once at a time; removed links are kept as history
            builder.HasIndex(l => new { l.ProjectId, l.RequirementId, l.WorkItemId })
                .IsUnique()
                .HasFilter("\"RemovedAt\" IS NULL");
        }
    }
}
