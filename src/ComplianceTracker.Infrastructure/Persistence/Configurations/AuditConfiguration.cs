using ComplianceTracker.Domain.Audits;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-134: "Audits" with their "AuditFindings", and the auditors' reports in "AuditReportFiles"
    public class AuditConfiguration : IEntityTypeConfiguration<Audit>
    {
        public void Configure(EntityTypeBuilder<Audit> builder)
        {
            builder.ToTable("Audits");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Result).HasConversion<string>().HasMaxLength(30);
            builder.Property(a => a.Scope).IsRequired().HasMaxLength(Audit.MaxTextLength);
            builder.Property(a => a.AuditorName).IsRequired().HasMaxLength(Audit.MaxTextLength);
            builder.Property(a => a.AuditorCompany).HasMaxLength(Audit.MaxTextLength);
            builder.Property(a => a.ReportFileName).IsRequired().HasMaxLength(255);
            builder.Property(a => a.ReportSha256).IsRequired().HasMaxLength(64);
            builder.Property(a => a.CertificateNumber).HasMaxLength(Audit.MaxTextLength);
            builder.Property(a => a.CertificateIssuer).HasMaxLength(Audit.MaxTextLength);
            builder.Ignore(a => a.HasCertificate);
            builder.Ignore(a => a.ShortScope);
            builder.HasOne<Project>().WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.RecordedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Audit>().WithMany().HasForeignKey(a => a.CorrectsAuditId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(a => a.Findings).WithOne().HasForeignKey(f => f.AuditId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Findings).HasField("_findings").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
            builder.HasIndex(a => a.ProjectId);
        }
    }

    public class AuditFindingConfiguration : IEntityTypeConfiguration<AuditFinding>
    {
        public void Configure(EntityTypeBuilder<AuditFinding> builder)
        {
            builder.ToTable("AuditFindings");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Title).IsRequired().HasMaxLength(Audit.MaxTextLength);
            builder.Property(f => f.CloseReason).HasMaxLength(AuditFinding.MaxReasonLength);
            builder.Ignore(f => f.IsOpen);
            builder.HasOne<User>().WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(f => f.ClosedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(f => f.ProjectId);
        }
    }

    public class AuditReportFileConfiguration : IEntityTypeConfiguration<AuditReportFile>
    {
        public void Configure(EntityTypeBuilder<AuditReportFile> builder)
        {
            builder.ToTable("AuditReportFiles");
            builder.HasKey(f => f.AuditId);
            builder.Property(f => f.AuditId).ValueGeneratedNever();
            builder.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<Audit>().WithOne().HasForeignKey<AuditReportFile>(f => f.AuditId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
