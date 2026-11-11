using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-119 R5: dismissed gaps, in "GapDismissals"
    public class GapDismissalConfiguration : IEntityTypeConfiguration<GapDismissal>
    {
        public void Configure(EntityTypeBuilder<GapDismissal> builder)
        {
            builder.ToTable("GapDismissals");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Key).IsRequired().HasMaxLength(GapDismissal.MaxKeyLength);
            builder.Property(d => d.Stamp).IsRequired().HasMaxLength(GapDismissal.MaxStampLength);
            builder.Property(d => d.Reason).IsRequired().HasMaxLength(1000);
            builder.Property(d => d.DismissedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.DismissedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(d => new { d.ProjectId, d.Key });
        }
    }
}
