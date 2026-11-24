using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-214: the assets in "Assets"; the projects, suppliers and risks they are linked to as arrays of ids, since an
    // asset is read whole and the links are few
    public class AssetConfiguration : IEntityTypeConfiguration<Asset>
    {
        public void Configure(EntityTypeBuilder<Asset> builder)
        {
            builder.ToTable("Assets");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Name).IsRequired().HasMaxLength(Asset.MaxNameLength);
            builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Description).HasMaxLength(Asset.MaxDescriptionLength);
            builder.Property(a => a.Classification).IsRequired().HasMaxLength(ClassificationScheme.MaxLevelLength);
            builder.Ignore(a => a.IsActive);

            builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(a => a.OrganizationId);
        }
    }

    // US-214 R2: one classification scheme per organisation, its levels lowest first
    public class ClassificationSchemeConfiguration : IEntityTypeConfiguration<ClassificationScheme>
    {
        public void Configure(EntityTypeBuilder<ClassificationScheme> builder)
        {
            builder.ToTable("ClassificationSchemes");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(s => s.OrganizationId).IsUnique();
        }
    }
}
