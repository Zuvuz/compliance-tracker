using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-183: the reminders in "Reminders". R2: one per person, kind, subject and deadline.
    public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
    {
        public void Configure(EntityTypeBuilder<Reminder> builder)
        {
            builder.ToTable("Reminders");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(r => r.LastError).HasMaxLength(Reminder.MaxErrorLength);
            builder.Ignore(r => r.IsSent);
            builder.Ignore(r => r.ShouldTry);

            builder.HasOne<Organization>().WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(r => new { r.UserId, r.Kind, r.SubjectId, r.Due }).IsUnique();
            builder.HasIndex(r => r.OrganizationId);
        }
    }
}
