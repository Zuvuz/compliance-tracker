using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class FrameworkVersionConfiguration : IEntityTypeConfiguration<FrameworkVersion>
    {
        public void Configure(EntityTypeBuilder<FrameworkVersion> builder)
        {
            builder.ToTable("FrameworkVersions");

            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Property(v => v.FileName).IsRequired().HasMaxLength(255);
            builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();

            // Versionsnummeret er unikt inden for et framework (1, 2, 3 ...)
            builder.HasIndex(v => new { v.FrameworkId, v.Number }).IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}