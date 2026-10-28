using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Organization (and its list of users) is stored in the table "Organizations"
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organizations");

            // The id is set by the domain (Guid.NewGuid), not by the database
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Property(o => o.Name).IsRequired();

            builder.Property(o => o.Cvr).IsRequired().HasMaxLength(8);
            // Safety net in the database behind the check in OrganizationService
            builder.HasIndex(o => o.Cvr).IsUnique();

            // Enums are stored as text, so new values never shift existing data
            builder.Property(o => o.Sector).HasConversion<string>().HasMaxLength(50);
            builder.Property(o => o.Size).HasConversion<string>().HasMaxLength(50);

            // The users belong to the organisation. The foreign key only exists in the database
            // (a "shadow property"), so the User class in the domain does not need it.
            builder.HasMany(o => o.Users)
                .WithOne()
                .HasForeignKey("OrganizationId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // EF writes straight into the private field _users, because Users is read-only.
            // AutoInclude loads the users every time an organisation is loaded.
            builder.Navigation(o => o.Users)
                .HasField("_users")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();
        }
    }
}
