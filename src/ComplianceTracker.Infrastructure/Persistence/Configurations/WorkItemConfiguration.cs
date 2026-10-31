using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How WorkItem is stored in the table "WorkItems" (US-018)
    public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
    {
        public void Configure(EntityTypeBuilder<WorkItem> builder)
        {
            builder.ToTable("WorkItems");

            // The id is set by the domain, not by the database
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).ValueGeneratedNever();

            // Stored as text, like all enums
            builder.Property(w => w.Source).HasConversion<string>().HasMaxLength(30);
            builder.Property(w => w.Type).HasConversion<string>().HasMaxLength(30);
            builder.Property(w => w.State).HasConversion<string>().HasMaxLength(20);

            builder.Property(w => w.ExternalReference).HasMaxLength(200).IsRequired();
            builder.Property(w => w.Url).HasMaxLength(500).IsRequired();
            builder.Property(w => w.Title).HasMaxLength(500).IsRequired();
            builder.Property(w => w.Labels).HasMaxLength(1000).IsRequired();

            // The template's sections can be long; text without a limit in PostgreSQL
            builder.Property(w => w.Rules).IsRequired();
            builder.Property(w => w.Examples).IsRequired();
            builder.Property(w => w.Gherkin).IsRequired();

            // Belongs to one project. Restrict: a project cannot be deleted while it has work items.
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // One work item per item in the source, e.g. one per GitHub issue
            builder.HasIndex(w => new { w.ProjectId, w.Source, w.ExternalReference }).IsUnique();
        }
    }
}
