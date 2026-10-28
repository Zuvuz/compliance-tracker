using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Framework is stored in the table "Frameworks"
    public class FrameworkConfiguration : IEntityTypeConfiguration<Framework>
    {
        public void Configure(EntityTypeBuilder<Framework> builder)
        {
            builder.ToTable("Frameworks");

            // The id is set by the domain (Guid.NewGuid), not by the database
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

            // Belongs to exactly one organisation (US-005 R7)
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(f => f.OwnerOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up "does this organisation already have a framework with this name?"
            builder.HasIndex(f => new { f.OwnerOrganizationId, f.Name });

            // The versions belong to the framework
            builder.HasMany(f => f.Versions)
                .WithOne()
                .HasForeignKey(v => v.FrameworkId)
                .OnDelete(DeleteBehavior.Restrict);

            // EF writes straight into the private field _versions, because Versions is read-only.
            // AutoInclude loads the versions every time a framework is loaded.
            builder.Navigation(f => f.Versions)
                .HasField("_versions")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();

            // CurrentVersion is calculated from Versions and not stored
            builder.Ignore(f => f.CurrentVersion);
        }
    }
}
