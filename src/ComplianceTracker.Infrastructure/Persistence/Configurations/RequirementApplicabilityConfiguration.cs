using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How RequirementApplicability is stored in the table "RequirementApplicability" (US-043)
    public class RequirementApplicabilityConfiguration : IEntityTypeConfiguration<RequirementApplicability>
    {
        public void Configure(EntityTypeBuilder<RequirementApplicability> builder)
        {
            builder.ToTable("RequirementApplicability");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Reason).IsRequired().HasMaxLength(1000);
            builder.Ignore(a => a.IsActive);

            builder.HasOne<Project>().WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(a => a.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.DecidedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.RevokedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // At most one active "not applicable" per requirement and project; revoked ones are kept as history
            builder.HasIndex(a => new { a.ProjectId, a.RequirementId })
                .IsUnique()
                .HasFilter("\"RevokedAt\" IS NULL");
        }
    }
}
