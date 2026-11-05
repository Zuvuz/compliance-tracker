using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How ProjectClassification is stored in the table "ProjectClassifications" (US-026). Rows are only added.
    public class ProjectClassificationConfiguration : IEntityTypeConfiguration<ProjectClassification>
    {
        public void Configure(EntityTypeBuilder<ProjectClassification> builder)
        {
            builder.ToTable("ProjectClassifications");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.Class).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Reason).IsRequired().HasMaxLength(ProjectClassification.MaxReasonLength);
            builder.Property(c => c.ClassifiedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasOne<Project>().WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Framework>().WithMany().HasForeignKey(c => c.FrameworkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(c => c.ClassifiedById).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => new { c.ProjectId, c.FrameworkId, c.ClassifiedAt });
        }
    }
}
