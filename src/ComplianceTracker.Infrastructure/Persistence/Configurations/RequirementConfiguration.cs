using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Requirement is stored in the table "Requirements" (US-007)
    public class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
    {
        public void Configure(EntityTypeBuilder<Requirement> builder)
        {
            builder.ToTable("Requirements");

            // The id is set by the domain, not by the database
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Title).IsRequired().HasMaxLength(500);
            builder.Property(r => r.Description).IsRequired().HasMaxLength(4000);

            // A code is used once per version (the domain checks it first, the index is the safety net)
            builder.HasIndex(r => new { r.FrameworkVersionId, r.Code }).IsUnique();
        }
    }
}
