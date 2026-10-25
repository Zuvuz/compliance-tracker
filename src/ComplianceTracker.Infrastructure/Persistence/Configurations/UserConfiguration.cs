using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();

            builder.Property(u => u.Name).IsRequired();

            // Keycloaks "sub": påkrævet og unik, så én login-identitet aldrig kan høre til to brugere (ASVS V10.5.2)
            builder.Property(u => u.ExternalId).IsRequired().HasMaxLength(255);
            builder.HasIndex(u => u.ExternalId).IsUnique();

            // 320 tegn er den længst mulige e-mailadresse
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        }
    }
}
