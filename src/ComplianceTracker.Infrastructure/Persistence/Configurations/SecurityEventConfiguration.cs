using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How SecurityEvent is stored in the table "SecurityEvents" (US-073)
    public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
    {
        public void Configure(EntityTypeBuilder<SecurityEvent> builder)
        {
            builder.ToTable("SecurityEvents");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(SecurityEvent.MaxTitleLength);
            builder.Property(e => e.Description).HasMaxLength(SecurityEvent.MaxDescriptionLength);
            builder.Property(e => e.RecordedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.EarlyWarningReference).HasMaxLength(SecurityEvent.MaxReferenceLength);
            builder.Property(e => e.NotificationReference).HasMaxLength(SecurityEvent.MaxReferenceLength);
            builder.Property(e => e.FinalReportReference).HasMaxLength(SecurityEvent.MaxReferenceLength);
            builder.Ignore(e => e.IsOpen);

            builder.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.ProjectId, e.AwareAt });
        }
    }
}
