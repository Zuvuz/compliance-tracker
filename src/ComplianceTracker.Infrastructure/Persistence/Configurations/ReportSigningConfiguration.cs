using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How signatures and seals are stored (US-132)
    public class ReportSigningConfiguration : IEntityTypeConfiguration<ReportSigning>
    {
        public void Configure(EntityTypeBuilder<ReportSigning> builder)
        {
            builder.ToTable("ReportSignings");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.SnapshotSha256).IsRequired().HasMaxLength(64);
            builder.Property(s => s.SealedSha256).HasMaxLength(64);
            builder.Property(s => s.SealSubject).HasMaxLength(500);
            builder.Property(s => s.SealThumbprint).HasMaxLength(100);
            builder.Ignore(s => s.IsSigned);
            builder.Ignore(s => s.IsSealed);
            builder.Ignore(s => s.Missing);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ReportSnapshot>().WithMany().HasForeignKey(s => s.SnapshotId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.RequestedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(s => s.SnapshotId).IsUnique();

            builder.HasMany(s => s.Signatures).WithOne().HasForeignKey(s => s.SigningId).OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(s => s.Signatures).HasField("_signatures").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class ReportSignatureConfiguration : IEntityTypeConfiguration<ReportSignature>
    {
        public void Configure(EntityTypeBuilder<ReportSignature> builder)
        {
            builder.ToTable("ReportSignatures");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Role).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.SignerRole).HasConversion<string>().HasMaxLength(50);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.SignerId).OnDelete(DeleteBehavior.Restrict);

            // R3 in the database too: each role once per report
            builder.HasIndex(s => new { s.SigningId, s.Role }).IsUnique();
        }
    }

    public class OrganisationSealConfiguration : IEntityTypeConfiguration<OrganisationSeal>
    {
        public void Configure(EntityTypeBuilder<OrganisationSeal> builder)
        {
            builder.ToTable("OrganisationSeals");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Subject).IsRequired().HasMaxLength(500);
            builder.Property(s => s.Thumbprint).IsRequired().HasMaxLength(100);
            builder.Property(s => s.ProtectedCertificate).IsRequired();
            builder.Property(s => s.ProtectedPassword).IsRequired();
            builder.Ignore(s => s.IsActive);
            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.UploadedById).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class SealedReportFileConfiguration : IEntityTypeConfiguration<SealedReportFile>
    {
        public void Configure(EntityTypeBuilder<SealedReportFile> builder)
        {
            builder.ToTable("SealedReportFiles");
            builder.HasKey(f => f.SigningId);
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<ReportSigning>().WithOne().HasForeignKey<SealedReportFile>(f => f.SigningId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
