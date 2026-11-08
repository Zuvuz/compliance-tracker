using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How locked reports are stored (US-050): "ReportSnapshots" and their files in "ReportSnapshotFiles". The migration
    // adds triggers so the database refuses to change or delete them (R2), like the activity log (US-091).
    public class ReportSnapshotConfiguration : IEntityTypeConfiguration<ReportSnapshot>
    {
        public void Configure(EntityTypeBuilder<ReportSnapshot> builder)
        {
            builder.ToTable("ReportSnapshots");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Kind).IsRequired().HasMaxLength(200);
            builder.Property(s => s.Title).IsRequired().HasMaxLength(ReportSnapshot.MaxTitleLength);
            builder.Property(s => s.FileName).IsRequired().HasMaxLength(255);
            builder.Property(s => s.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Sha256).IsRequired().HasMaxLength(64);
            builder.Property(s => s.LockedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.LockedById).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.OrganizationId, s.Sha256 });
            builder.HasIndex(s => s.ProjectId);
        }
    }

    public class ReportSnapshotFileConfiguration : IEntityTypeConfiguration<ReportSnapshotFile>
    {
        public void Configure(EntityTypeBuilder<ReportSnapshotFile> builder)
        {
            builder.ToTable("ReportSnapshotFiles");
            builder.HasKey(f => f.SnapshotId);
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<ReportSnapshot>().WithOne().HasForeignKey<ReportSnapshotFile>(f => f.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
