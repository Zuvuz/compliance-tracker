using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How the PDFs are stored in the table "CatalogDocuments"
    public class CatalogDocumentConfiguration : IEntityTypeConfiguration<CatalogDocument>
    {
        public void Configure(EntityTypeBuilder<CatalogDocument> builder)
        {
            builder.ToTable("CatalogDocuments");

            // One PDF per version: the version's id is also the document's key
            builder.HasKey(d => d.FrameworkVersionId);
            builder.Property(d => d.Content).IsRequired(); // stored as bytea in PostgreSQL

            // Restrict: a version cannot be deleted while its PDF exists
            builder.HasOne<FrameworkVersion>()
                .WithOne()
                .HasForeignKey<CatalogDocument>(d => d.FrameworkVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
