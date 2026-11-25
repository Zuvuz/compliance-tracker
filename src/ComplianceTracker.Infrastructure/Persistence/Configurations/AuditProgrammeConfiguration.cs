using System.Text.Json;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-211: the audit programme in "AuditProgrammes" (one per organisation, the control owners as JSON) and its planned
    // audits in "PlannedAudits" (clauses and controls as text arrays, projects as uuid arrays)
    public class AuditProgrammeConfiguration : IEntityTypeConfiguration<AuditProgramme>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<AuditProgramme> builder)
        {
            builder.ToTable("AuditProgrammes");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Ignore(p => p.EndsOn);
            builder.Property(p => p.ControlOwners)
                .HasColumnType("jsonb")
                .HasConversion(
                    list => JsonSerializer.Serialize(list, Json),
                    json => JsonSerializer.Deserialize<List<ControlOwner>>(json, Json) ?? new List<ControlOwner>(),
                    new ValueComparer<List<ControlOwner>>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c.ToList()));

            builder.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(p => p.OrganizationId).IsUnique();

            builder.HasMany(p => p.Audits).WithOne().HasForeignKey(a => a.ProgrammeId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(p => p.Audits).HasField("_audits").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class PlannedAuditConfiguration : IEntityTypeConfiguration<PlannedAudit>
    {
        public void Configure(EntityTypeBuilder<PlannedAudit> builder)
        {
            builder.ToTable("PlannedAudits");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.AuditorName).HasMaxLength(200);
            builder.Property(a => a.ImpartialityReason).HasMaxLength(1000);
            builder.Ignore(a => a.IsDone);
            builder.HasIndex(a => a.ProgrammeId);
        }
    }
}
