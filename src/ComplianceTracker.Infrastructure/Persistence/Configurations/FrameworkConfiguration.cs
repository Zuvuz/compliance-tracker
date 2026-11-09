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

            // US-142: the level, and the directive a national law implements in a country
            builder.Property(f => f.Level).HasConversion<string>().HasMaxLength(20).HasDefaultValue(FrameworkLevel.Product);
            builder.Property(f => f.Country).HasMaxLength(2);
            builder.HasOne<Framework>()
                .WithMany()
                .HasForeignKey(f => f.ImplementsFrameworkId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Ignore(f => f.ShortName);

            // An uploaded framework belongs to exactly one organisation (US-005 R7).
            // Built-in frameworks have no owner, so the foreign key is optional (US-006).
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(f => f.OwnerOrganizationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // IsBuiltIn is calculated from OwnerOrganizationId and not stored
            builder.Ignore(f => f.IsBuiltIn);

            // The built-in frameworks that come with every database (US-006)
            builder.HasData(
                BuiltIn(BuiltInFrameworks.Cra.FrameworkId, BuiltInFrameworks.Cra.Name, FrameworkLevel.Product),
                BuiltIn(BuiltInFrameworks.AiAct.FrameworkId, BuiltInFrameworks.AiAct.Name, FrameworkLevel.Product),
                // US-142
                BuiltIn(BuiltInFrameworks.Nis2.FrameworkId, BuiltInFrameworks.Nis2.Name, FrameworkLevel.Organisation),
                BuiltIn(BuiltInFrameworks.Nis2Denmark.FrameworkId, BuiltInFrameworks.Nis2Denmark.Name, FrameworkLevel.Organisation,
                    BuiltInFrameworks.Nis2.FrameworkId, BuiltInFrameworks.Nis2Denmark.Country),
                BuiltIn(BuiltInFrameworks.Nis2Technical.FrameworkId, BuiltInFrameworks.Nis2Technical.Name, FrameworkLevel.Organisation,
                    BuiltInFrameworks.Nis2.FrameworkId));

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

            // LatestVersion is calculated from Versions and not stored (CurrentVersion is a method, US-035)
            builder.Ignore(f => f.LatestVersion);
        }

        private static object BuiltIn(Guid id, string name, FrameworkLevel level, Guid? implements = null, string? country = null)
            => new
            {
                Id = id,
                OwnerOrganizationId = (Guid?)null,
                Name = name,
                Level = level,
                ImplementsFrameworkId = implements,
                Country = country
            };
    }
}
