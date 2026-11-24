using System.Text.Json;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-212: the nonconformities in "Nonconformities"; the corrective actions that did not work as JSON
    public class NonconformityConfiguration : IEntityTypeConfiguration<Nonconformity>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<Nonconformity> builder)
        {
            builder.ToTable("Nonconformities");
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Id).ValueGeneratedNever();
            builder.Property(n => n.Title).IsRequired().HasMaxLength(Nonconformity.MaxTitleLength);
            builder.Property(n => n.Description).HasMaxLength(Nonconformity.MaxTextLength);
            builder.Property(n => n.Correction).HasMaxLength(Nonconformity.MaxTextLength);
            builder.Property(n => n.Cause).HasMaxLength(Nonconformity.MaxTextLength);
            builder.Property(n => n.Action).HasMaxLength(Nonconformity.MaxTextLength);
            builder.Property(n => n.CheckResult).HasMaxLength(Nonconformity.MaxTextLength);
            builder.Property(n => n.Source).HasConversion<string>().HasMaxLength(30);
            builder.Property(n => n.Severity).HasConversion<string>().HasMaxLength(20);
            builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(n => n.IsClosed);
            builder.Property(n => n.FailedActions)
                .HasColumnType("jsonb")
                .HasConversion(
                    list => JsonSerializer.Serialize(list, Json),
                    json => JsonSerializer.Deserialize<List<FailedAction>>(json, Json) ?? new List<FailedAction>(),
                    new ValueComparer<List<FailedAction>>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c.ToList()));

            builder.HasOne<Organization>().WithMany().HasForeignKey(n => n.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(n => n.OrganizationId);
        }
    }
}
