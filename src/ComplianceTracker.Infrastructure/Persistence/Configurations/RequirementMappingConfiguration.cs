using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-037: the cross-mappings between two framework versions, in "RequirementMappings"
    public class RequirementMappingConfiguration : IEntityTypeConfiguration<RequirementMapping>
    {
        public void Configure(EntityTypeBuilder<RequirementMapping> builder)
        {
            builder.ToTable("RequirementMappings");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Strength).HasConversion<string>().HasMaxLength(20);
            builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(m => m.RejectionReason).HasConversion<string>().HasMaxLength(50);
            builder.Property(m => m.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(m => m.Rationale).IsRequired().HasMaxLength(500);
            builder.Property(m => m.Model).IsRequired().HasMaxLength(200);
            builder.Property(m => m.Comment).HasMaxLength(1000);
            builder.Ignore(m => m.IsPending);
            builder.Ignore(m => m.IsApprovedSame);
            builder.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(m => m.FromRequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(m => m.ToRequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(m => m.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(m => new { m.OrganizationId, m.FromVersionId, m.ToVersionId });
        }
    }
}
