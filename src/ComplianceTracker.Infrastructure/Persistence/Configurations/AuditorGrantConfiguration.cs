using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-182: "AuditorGrants"
    public class AuditorGrantConfiguration : IEntityTypeConfiguration<AuditorGrant>
    {
        public void Configure(EntityTypeBuilder<AuditorGrant> builder)
        {
            builder.ToTable("AuditorGrants");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.Id).ValueGeneratedNever();
            builder.Property(g => g.Email).IsRequired().HasMaxLength(AuditorGrant.MaxEmailLength);
            builder.Ignore(g => g.IsRevoked);

            builder.HasOne<Organization>().WithMany().HasForeignKey(g => g.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Project>().WithMany().HasForeignKey(g => g.ProjectId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(g => g.InvitationId).IsUnique();
            builder.HasIndex(g => g.OrganizationId);
        }
    }
}
