using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-040: the texts of a project's management report, one per part (the summary, or a regulation)
    public class StatusTextConfiguration : IEntityTypeConfiguration<StatusText>
    {
        public void Configure(EntityTypeBuilder<StatusText> builder)
        {
            builder.ToTable("StatusTexts");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Part).IsRequired().HasMaxLength(300);
            builder.Property(t => t.Text).IsRequired().HasMaxLength(StatusText.MaxLength);
            builder.Property(t => t.Basis).IsRequired().HasMaxLength(64);
            builder.Property(t => t.Model).HasMaxLength(200);
            builder.Ignore(t => t.IsSummary);

            builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(t => t.ChangedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(t => new { t.ProjectId, t.Part }).IsUnique();
        }
    }
}
