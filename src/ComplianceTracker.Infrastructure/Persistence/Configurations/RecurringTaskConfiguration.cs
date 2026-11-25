using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-215: the yearly plan's tasks in "RecurringTasks" (controls as a text array) and their completions in "TaskCompletions"
    public class RecurringTaskConfiguration : IEntityTypeConfiguration<RecurringTask>
    {
        public void Configure(EntityTypeBuilder<RecurringTask> builder)
        {
            builder.ToTable("RecurringTasks");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Title).IsRequired().HasMaxLength(RecurringTask.MaxTitleLength);
            builder.Property(t => t.Description).HasMaxLength(RecurringTask.MaxTextLength);
            builder.Property(t => t.EvidenceNeeded).HasMaxLength(RecurringTask.MaxTextLength);
            builder.Property(t => t.SuggestionKey).HasMaxLength(50);
            builder.Property(t => t.Interval).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(t => t.IsActive);
            builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(t => t.OrganizationId);
            builder.HasMany(t => t.Completions).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(t => t.Completions).HasField("_completions").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class TaskCompletionConfiguration : IEntityTypeConfiguration<TaskCompletion>
    {
        public void Configure(EntityTypeBuilder<TaskCompletion> builder)
        {
            builder.ToTable("TaskCompletions");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.Note).HasMaxLength(RecurringTask.MaxTextLength);
            builder.Ignore(c => c.WasLate);
            builder.HasIndex(c => c.TaskId);
        }
    }
}
