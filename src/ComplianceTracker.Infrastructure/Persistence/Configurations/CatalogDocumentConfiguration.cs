using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class CatalogDocumentConfiguration : IEntityTypeConfiguration<CatalogDocument>
    {
        public void Configure(EntityTypeBuilder<CatalogDocument> builder)
        {
            builder.ToTable("CatalogDocuments");

            // Én PDF pr. version: versionens id er også dokumentets nøgle
            builder.HasKey(d => d.FrameworkVersionId);
            builder.Property(d => d.Content).IsRequired(); // bytea i PostgreSQL

            builder.HasOne<FrameworkVersion>()
                .WithOne()
                .HasForeignKey<CatalogDocument>(d => d.FrameworkVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}