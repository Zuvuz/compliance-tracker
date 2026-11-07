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
