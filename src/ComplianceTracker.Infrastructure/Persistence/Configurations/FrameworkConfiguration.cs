using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class FrameworkConfiguration : IEntityTypeConfiguration<Framework>
    {
        public void Configure(EntityTypeBuilder<Framework> builder)
        {
            builder.ToTable("Frameworks");

            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

            // Hører til præcis én organisation (US-005 R7)
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(f => f.OwnerOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(f => new { f.OwnerOrganizationId, f.Name });

            // Versionerne hører til frameworket. EF skriver i det private felt _versions.
            builder.HasMany(f => f.Versions)
                .WithOne()
                .HasForeignKey(v => v.FrameworkId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(f => f.Versions)
                .HasField("_versions")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();

            // CurrentVersion udregnes ud fra Versions og gemmes ikke
            builder.Ignore(f => f.CurrentVersion);
        }
    }
}