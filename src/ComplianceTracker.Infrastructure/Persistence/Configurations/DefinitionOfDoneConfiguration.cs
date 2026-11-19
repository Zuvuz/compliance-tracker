using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-109: a project's Definition of Done in "DefinitionsOfDone" (one per project), and the manual ticks in "DoneTicks"
    public class DefinitionOfDoneConfiguration : IEntityTypeConfiguration<DefinitionOfDone>
    {
        public void Configure(EntityTypeBuilder<DefinitionOfDone> builder)
        {
            builder.ToTable("DefinitionsOfDone");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.UserStoryCriteria).IsRequired().HasMaxLength(2000);
            builder.Property(d => d.UseCaseCriteria).IsRequired().HasMaxLength(2000);
            builder.Property(d => d.RequirementCriteria).IsRequired().HasMaxLength(2000);
            builder.Property(d => d.ChangedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(d => d.All);
            builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.ChangedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(d => d.ProjectId).IsUnique();
        }
    }

    public class DoneTickConfiguration : IEntityTypeConfiguration<DoneTick>
    {
        public void Configure(EntityTypeBuilder<DoneTick> builder)
        {
            builder.ToTable("DoneTicks");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Criterion).IsRequired().HasMaxLength(DefinitionOfDone.MaxTitleLength);
            builder.Property(t => t.TickedRole).HasConversion<string>().HasMaxLength(50);
            builder.Ignore(t => t.IsActive);
            builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(t => t.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(t => t.TickedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(t => t.UntickedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(t => t.ProjectId);
        }
    }
}
