using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-191: the suppliers in "Suppliers", their evidence in "SupplierEvidence", and the files in "SupplierEvidenceFiles"
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.ToTable("Suppliers");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Name).IsRequired().HasMaxLength(Supplier.MaxNameLength);
            builder.Property(s => s.Delivers).IsRequired().HasMaxLength(Supplier.MaxTextLength);
            builder.Property(s => s.Contact).HasMaxLength(Supplier.MaxTextLength);
            builder.Property(s => s.Rules).IsRequired().HasMaxLength(Supplier.MaxTextLength);
            builder.Property(s => s.Criticality).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(s => s.IsActive);
            builder.Ignore(s => s.RuleList);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(s => s.OrganizationId);

            builder.HasMany(s => s.Evidence).WithOne().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Evidence).HasField("_evidence").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class SupplierEvidenceConfiguration : IEntityTypeConfiguration<SupplierEvidence>
    {
        public void Configure(EntityTypeBuilder<SupplierEvidence> builder)
        {
            builder.ToTable("SupplierEvidence");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(Supplier.MaxNameLength);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);
        }
    }

    public class SupplierEvidenceFileConfiguration : IEntityTypeConfiguration<SupplierEvidenceFile>
    {
        public void Configure(EntityTypeBuilder<SupplierEvidenceFile> builder)
        {
            builder.ToTable("SupplierEvidenceFiles");
            builder.HasKey(f => f.EvidenceId);
            builder.Property(f => f.EvidenceId).ValueGeneratedNever();
            builder.HasOne<SupplierEvidence>().WithOne().HasForeignKey<SupplierEvidenceFile>(f => f.EvidenceId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
