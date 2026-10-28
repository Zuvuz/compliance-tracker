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

            // Keycloak's "sub": required and unique, so one sign-in identity can never belong to two users (ASVS V10.5.2)
            builder.Property(u => u.ExternalId).IsRequired().HasMaxLength(255);
            builder.HasIndex(u => u.ExternalId).IsUnique();

            // 320 characters is the longest possible e-mail address
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);

            // Stored as text, like all enums
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        }
    }
}
