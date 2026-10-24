using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organizations");

            // Id sættes af domænet (Guid.NewGuid), ikke af databasen
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Property(o => o.Name).IsRequired();

            builder.Property(o => o.Cvr).IsRequired().HasMaxLength(8);
            // Sikkerhedsnet i databasen bag tjekket i OrganizationService
            builder.HasIndex(o => o.Cvr).IsUnique();

            // Enums gemmes som tekst, så nye værdier ikke forskyder eksisterende data
            builder.Property(o => o.Sector).HasConversion<string>().HasMaxLength(50);
            builder.Property(o => o.Size).HasConversion<string>().HasMaxLength(50);

            // Brugerne hører til organisationen. Fremmednøglen findes kun i databasen (shadow property),
            // så User-klassen i domænet ikke skal ændres.
            builder.HasMany(o => o.Users)
                .WithOne()
                .HasForeignKey("OrganizationId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // EF skriver direkte i det private felt _users, fordi Users er read-only udadtil.
            // AutoInclude henter brugerne med, hver gang en organisation hentes.
            builder.Navigation(o => o.Users)
                .HasField("_users")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();
        }
    }
}
