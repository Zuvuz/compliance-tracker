using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How SecurityEventSuggestion is stored in the table "SecurityEventSuggestions" (US-128)
    public class SecurityEventSuggestionConfiguration : IEntityTypeConfiguration<SecurityEventSuggestion>
    {
        public void Configure(EntityTypeBuilder<SecurityEventSuggestion> builder)
        {
            builder.ToTable("SecurityEventSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Key).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Title).IsRequired().HasMaxLength(SecurityEvent.MaxTitleLength);
            builder.Property(s => s.Source).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.SourceName).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.DismissedReason).HasMaxLength(SecurityEventSuggestion.MaxReasonLength);
            builder.Ignore(s => s.IsPending);
            builder.Ignore(s => s.IsUpgrade);

            builder.HasOne<Project>().WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SecurityEvent>().WithMany().HasForeignKey(s => s.UpgradeEventId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SecurityEvent>().WithMany().HasForeignKey(s => s.EventId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ProjectId, s.Key });
        }
    }

    // The exploited-vulnerability lists as last fetched (US-128 R5); system-wide, not per organisation
    public class ExploitedListConfiguration : IEntityTypeConfiguration<ExploitedList>
    {
        public void Configure(EntityTypeBuilder<ExploitedList> builder)
        {
            builder.ToTable("ExploitedLists");
            builder.HasKey(l => l.Name);
            builder.Property(l => l.Name).HasMaxLength(100);
            builder.Property(l => l.LastError).HasMaxLength(ExploitedList.MaxErrorLength);
        }
    }
}
