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

            // Restrict: an organisation or user cannot be deleted while log entries point at it
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
