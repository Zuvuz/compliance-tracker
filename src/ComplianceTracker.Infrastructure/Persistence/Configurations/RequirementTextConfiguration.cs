using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-142 R8: how RequirementText is stored in the table "RequirementTexts"
    public class RequirementTextConfiguration : IEntityTypeConfiguration<RequirementText>
    {
        public void Configure(EntityTypeBuilder<RequirementText> builder)
        {
            builder.ToTable("RequirementTexts");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Language).IsRequired().HasMaxLength(RequirementText.LanguageLength);
            builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
            builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

            // One text per language per requirement
            builder.HasIndex(t => new { t.RequirementId, t.Language }).IsUnique();

            // The Danish texts of the built-in NIS2 catalogues
            foreach (var (catalogue, _) in RequirementConfiguration.Nis2Catalogues)
            {
                builder.HasData(catalogue.Select(item => new
                {
                    Id = item.DanishTextId,
                    RequirementId = item.Id,
                    Language = "da",
                    Title = item.DanishTitle,
                    Description = item.DanishDescription
                }));
            }
        }
    }
}
