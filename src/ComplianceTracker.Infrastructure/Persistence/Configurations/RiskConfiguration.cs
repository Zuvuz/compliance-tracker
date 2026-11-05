using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Risk is stored in the table "Risks", and its measures in "RiskMeasures" (US-042)
    public class RiskConfiguration : IEntityTypeConfiguration<Risk>
    {
        public void Configure(EntityTypeBuilder<Risk> builder)
        {
            builder.ToTable("Risks");

            // US-092: PostgreSQL's xmin as the row version; a save fails if someone else changed the row since it was read
            builder.Property(x => x.Version).IsRowVersion();

            // US-010: the role when the decision was made; never changed afterwards
            builder.Property(r => r.AcceptedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
            builder.Property(r => r.Description).IsRequired().HasMaxLength(4000);
            builder.Property(r => r.Treatment).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.AcceptedReason).HasMaxLength(1000);
            builder.Property(r => r.ClosedReason).HasMaxLength(1000);

            // Calculated, not stored
            builder.Ignore(r => r.Score);
            builder.Ignore(r => r.Level);
            builder.Ignore(r => r.ResidualLevel);
            builder.Ignore(r => r.HasNoMeasures);
            builder.Ignore(r => r.DisplayStatus);

            // Restrict: a project or user cannot be deleted while risks point at it
            builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.AcceptedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.LastReviewedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // The measures belong to the risk; EF writes straight into the private field and loads them with the risk
            builder.HasMany(r => r.Measures).WithOne().HasForeignKey(m => m.RiskId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Measures).HasField("_measures").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

            builder.HasIndex(r => r.ProjectId);
        }
    }

    public class RiskMeasureConfiguration : IEntityTypeConfiguration<RiskMeasure>
    {
        public void Configure(EntityTypeBuilder<RiskMeasure> builder)
        {
            builder.ToTable("RiskMeasures");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.HasOne<Requirement>().WithMany().HasForeignKey(m => m.RequirementId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(m => m.WorkItemId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
