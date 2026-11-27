using ComplianceTracker.Domain.ActivityLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-219: how PageVisit is stored in the table "PageVisits"
    public class PageVisitConfiguration : IEntityTypeConfiguration<PageVisit>
    {
        public void Configure(EntityTypeBuilder<PageVisit> builder)
        {
            builder.ToTable("PageVisits");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Property(v => v.Role).HasConversion<string>().HasMaxLength(50);
            builder.Property(v => v.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(v => v.Tab).HasMaxLength(50);
            builder.Property(v => v.Title).HasMaxLength(300);

            // R5: the organisation's log and a project's by time; R3: who is on a page now; "Seen by" per item;
            // R6: deleting by the start time
            builder.HasIndex(v => new { v.OrganizationId, v.StartedAt });
            builder.HasIndex(v => new { v.OrganizationId, v.ProjectId, v.LastSeenAt });
            builder.HasIndex(v => new { v.OrganizationId, v.Kind, v.ItemId });
            builder.HasIndex(v => new { v.OrganizationId, v.UserId, v.StartedAt });
        }
    }
}
