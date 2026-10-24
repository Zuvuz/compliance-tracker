using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    public class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
    {
        public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
        {
            builder.ToTable("ActivityLog");

            // ActivityLogEntry har intet Id i domænet. Databasen giver hver logpost et
            // fortløbende nummer, som kun findes i databasen (shadow key).
            builder.Property<long>("Id").UseIdentityAlwaysColumn();
            builder.HasKey("Id");

            // Properties uden setter skal nævnes eksplicit, før EF gemmer dem.
            // EF udfylder dem via constructoren, når en logpost hentes.
            builder.Property(e => e.OrganizationId);
            builder.Property(e => e.UserId);
            builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.OccurredAt);

            // Restrict: en organisation eller bruger kan ikke slettes, så længe der findes logposter
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
