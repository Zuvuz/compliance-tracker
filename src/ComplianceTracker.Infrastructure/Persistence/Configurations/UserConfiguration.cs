using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How User is stored in the table "Users"
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            // The id is set by the domain, not by the database
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();

            builder.Property(u => u.Name).IsRequired();

            // Keycloak's "sub": required and unique among an organisation's members, so one sign-in identity can never
            // be two members of the same organisation (ASVS V10.5.2). US-187: the same person can be a member of several
            // organisations with an external role (B121). US-090: a removed member's row is kept.
            builder.Property(u => u.ExternalId).IsRequired().HasMaxLength(255);
            builder.HasIndex("OrganizationId", nameof(User.ExternalId)).IsUnique().HasFilter("\"RemovedAt\" IS NULL");
            builder.HasIndex(u => u.ExternalId); // finds the person's organisations at every request
            builder.Ignore(u => u.IsActive);

            // US-096: existing members see the card until they hide it
            builder.Property(u => u.HidesGetStarted).HasDefaultValue(false);

            // 320 characters is the longest possible e-mail address
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);

            // Stored as text, like all enums
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        }
    }
}
