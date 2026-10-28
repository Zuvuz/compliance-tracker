using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How FrameworkVersion is stored in the table "FrameworkVersions" (the PDF itself is in CatalogDocuments)
    public class FrameworkVersionConfiguration : IEntityTypeConfiguration<FrameworkVersion>
    {
        public void Configure(EntityTypeBuilder<FrameworkVersion> builder)
        {
            builder.ToTable("FrameworkVersions");

            // The id is set by the domain, not by the database
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Property(v => v.FileName).IsRequired().HasMaxLength(255);

            // A SHA-256 hash written as hex is always 64 characters
            builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();

            // The version number is unique within a framework (1, 2, 3 ...)
            builder.HasIndex(v => new { v.FrameworkId, v.Number }).IsUnique();

            // Who uploaded it. Restrict: a user cannot be deleted while they have uploaded versions.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
