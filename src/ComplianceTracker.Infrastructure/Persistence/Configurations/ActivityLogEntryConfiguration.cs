using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How ActivityLogEntry is stored in the table "ActivityLog"
    public class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
    {
        public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
        {
            builder.ToTable("ActivityLog");

            // ActivityLogEntry has no Id in the domain. The database gives each entry
            // a running number that only exists in the database (a "shadow key").
            builder.Property<long>("Id").UseIdentityAlwaysColumn();
            builder.HasKey("Id");

            // Properties without a setter must be listed explicitly, or EF does not store them.
            // EF fills them in through the constructor when it reads an entry.
            builder.Property(e => e.OrganizationId);
            builder.Property(e => e.UserId);
            builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.OccurredAt);
            builder.Property(e => e.SubjectId);

            // US-024: old and new values; long enough for a changed intended purpose
            builder.Property(e => e.Details).HasMaxLength(4000);

            // Restrict: an organisation or user cannot be deleted while log entries point at it
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional: null means the system did it (US-018)
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
