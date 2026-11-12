using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-078: "Scans", their files in "ScanFiles", and "Findings"
    public class ScanConfiguration : IEntityTypeConfiguration<Scan>
    {
        public void Configure(EntityTypeBuilder<Scan> builder)
        {
            builder.ToTable("Scans");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Tool).IsRequired().HasMaxLength(200);
            builder.Property(s => s.FileName).IsRequired().HasMaxLength(255);
            builder.Property(s => s.Sha256).IsRequired().HasMaxLength(64);
            builder.Ignore(s => s.Summary);
            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.UploadedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.Property(s => s.Source).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ScanSource.Upload).HasSentinel(ScanSource.Upload); // US-121
            builder.HasIndex(s => new { s.ProjectId, s.ScannedAt });

            // US-122: what a pipeline built, and its token
            builder.Property(s => s.CommitSha).HasMaxLength(40);
            builder.Property(s => s.Version).HasMaxLength(Scan.MaxVersionLength);
            builder.Ignore(s => s.Build);
            builder.HasOne<ScanUploadToken>().WithMany().HasForeignKey(s => s.UploadTokenId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(s => new { s.UploadTokenId, s.ScannedAt });
        }
    }

    // US-079: "Sboms" with their "SbomComponents", and the files in "SbomFiles"
    public class SbomConfiguration : IEntityTypeConfiguration<Sbom>
    {
        public void Configure(EntityTypeBuilder<Sbom> builder)
        {
            builder.ToTable("Sboms");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.ProductVersion).IsRequired().HasMaxLength(Sbom.MaxVersionLength);
            builder.Property(s => s.Format).IsRequired().HasMaxLength(50);
            builder.Property(s => s.FileName).IsRequired().HasMaxLength(255);
            builder.Property(s => s.Sha256).IsRequired().HasMaxLength(64);
            builder.Ignore(s => s.Summary);
            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.UploadedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(s => new { s.ProjectId, s.ImportedAt });
            builder.HasMany(s => s.Components).WithOne().HasForeignKey(c => c.SbomId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Components).HasField("_components").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class SbomComponentConfiguration : IEntityTypeConfiguration<SbomComponent>
    {
        public void Configure(EntityTypeBuilder<SbomComponent> builder)
        {
            builder.ToTable("SbomComponents");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.Name).IsRequired().HasMaxLength(300);
            builder.Property(c => c.Version).HasMaxLength(200);
            builder.Property(c => c.PackageUrl).HasMaxLength(1000);
            builder.Property(c => c.License).HasMaxLength(300);
            builder.Property(c => c.Supplier).HasMaxLength(300);
        }
    }

    public class SbomFileConfiguration : IEntityTypeConfiguration<SbomFile>
    {
        public void Configure(EntityTypeBuilder<SbomFile> builder)
        {
            builder.ToTable("SbomFiles");
            builder.HasKey(f => f.SbomId);
            builder.Property(f => f.SbomId).ValueGeneratedNever();
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<Sbom>().WithOne().HasForeignKey<SbomFile>(f => f.SbomId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    // US-122: only the hash of a token is stored, and it is unique
    public class ScanUploadTokenConfiguration : IEntityTypeConfiguration<ScanUploadToken>
    {
        public void Configure(EntityTypeBuilder<ScanUploadToken> builder)
        {
            builder.ToTable("ScanUploadTokens");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Name).IsRequired().HasMaxLength(ScanUploadToken.MaxNameLength);
            builder.Property(t => t.Hash).IsRequired().HasMaxLength(64);
            builder.Ignore(t => t.IsActive);
            builder.HasIndex(t => t.Hash).IsUnique();
            builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(t => t.RevokedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ScanFileConfiguration : IEntityTypeConfiguration<ScanFile>
    {
        public void Configure(EntityTypeBuilder<ScanFile> builder)
        {
            builder.ToTable("ScanFiles");
            builder.HasKey(f => f.ScanId);
            builder.Property(f => f.ScanId).ValueGeneratedNever();
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<Scan>().WithOne().HasForeignKey<ScanFile>(f => f.ScanId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FindingConfiguration : IEntityTypeConfiguration<Finding>
    {
        public void Configure(EntityTypeBuilder<Finding> builder)
        {
            builder.ToTable("Findings");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Tool).IsRequired().HasMaxLength(200);
            builder.Property(f => f.Fingerprint).IsRequired().HasMaxLength(200);
            builder.Property(f => f.RuleId).IsRequired().HasMaxLength(200);
            builder.Property(f => f.Severity).HasConversion<string>().HasMaxLength(20);
            builder.Property(f => f.State).HasConversion<string>().HasMaxLength(20);
            builder.Property(f => f.Message).IsRequired().HasMaxLength(2000);
            builder.Property(f => f.Location).HasMaxLength(500);
            builder.Property(f => f.AcceptedReason).HasMaxLength(Finding.MaxReasonLength);
            builder.Ignore(f => f.IsOpen);
            builder.HasOne<Project>().WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(f => f.AcceptedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(f => new { f.ProjectId, f.Tool, f.Fingerprint }).IsUnique();
        }
    }
}
