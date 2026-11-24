using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-209: the objectives in "SecurityObjectives" and their measurements in "ObjectiveMeasurements"
    public class SecurityObjectiveConfiguration : IEntityTypeConfiguration<SecurityObjective>
    {
        public void Configure(EntityTypeBuilder<SecurityObjective> builder)
        {
            builder.ToTable("SecurityObjectives");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();
            builder.Property(o => o.Text).IsRequired().HasMaxLength(SecurityObjective.MaxTextLength);
            builder.Property(o => o.Unit).IsRequired().HasMaxLength(SecurityObjective.MaxUnitLength);
            builder.Property(o => o.Target).HasPrecision(12, 2);
            builder.Property(o => o.Measure).HasConversion<string>().HasMaxLength(30);
            builder.Property(o => o.Direction).HasConversion<string>().HasMaxLength(20);
            builder.Property(o => o.Frequency).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(o => o.IsActive);
            builder.Ignore(o => o.IsManual);
            builder.Ignore(o => o.Last);
            builder.Ignore(o => o.NextMeasurementOn);
            builder.Ignore(o => o.State);

            builder.HasOne<Organization>().WithMany().HasForeignKey(o => o.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(o => o.OrganizationId);

            builder.HasMany(o => o.Measurements).WithOne().HasForeignKey(m => m.ObjectiveId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(o => o.Measurements).HasField("_measurements").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
    {
        public void Configure(EntityTypeBuilder<Measurement> builder)
        {
            builder.ToTable("ObjectiveMeasurements");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Value).HasPrecision(12, 2);
            builder.Property(m => m.Note).HasMaxLength(SecurityObjective.MaxNoteLength);
            builder.Ignore(m => m.ByTracker);
            builder.HasIndex(m => m.ObjectiveId);
        }
    }
}
