using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Risk is stored in the table "Risks", and its measures in "RiskMeasures" (US-042)
    public class RiskConfiguration : IEntityTypeConfiguration<Risk>
    {
        public void Configure(EntityTypeBuilder<Risk> builder)
        {
            builder.ToTable("Risks");

            // US-092: PostgreSQL's xmin as the row version; a save fails if someone else changed the row since it was read
            builder.Property(x => x.Version).IsRowVersion();

            // US-010: the role when the decision was made; never changed afterwards
            builder.Property(r => r.AcceptedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
            builder.Property(r => r.Description).IsRequired().HasMaxLength(4000);
            builder.Property(r => r.Treatment).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.AcceptedReason).HasMaxLength(1000);
            builder.Property(r => r.ClosedReason).HasMaxLength(1000);

            // Calculated, not stored
            builder.Ignore(r => r.Score);
            builder.Ignore(r => r.Level);
            builder.Ignore(r => r.ResidualLevel);
            builder.Ignore(r => r.HasNoMeasures);
            builder.Ignore(r => r.DisplayStatus);

            // Restrict: a project or user cannot be deleted while risks point at it
            builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.AcceptedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.LastReviewedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // The measures belong to the risk; EF writes straight into the private field and loads them with the risk
            builder.HasMany(r => r.Measures).WithOne().HasForeignKey(m => m.RiskId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Measures).HasField("_measures").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

            // US-111: the treatment actions, loaded with the risk like the measures
            builder.HasMany(r => r.Actions).WithOne().HasForeignKey(a => a.RiskId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Actions).HasField("_actions").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

            builder.HasIndex(r => r.ProjectId);

            // US-049: where an imported risk came from
            builder.Property(r => r.ImportedFrom).HasMaxLength(255);
            builder.Property(r => r.ImportedOwner).HasMaxLength(200);
        }
    }

    // US-049: imported risk registers, and their files in "RiskImportFiles"
    public class RiskImportConfiguration : IEntityTypeConfiguration<RiskImport>
    {
        public void Configure(EntityTypeBuilder<RiskImport> builder)
        {
            builder.ToTable("RiskImports");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.FileName).IsRequired().HasMaxLength(255);
            builder.Property(i => i.Sha256).IsRequired().HasMaxLength(64);
            builder.Ignore(i => i.IsConfirmed);
            builder.HasOne<Project>().WithMany().HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(i => i.ProjectId);
        }
    }

    public class RiskImportFileConfiguration : IEntityTypeConfiguration<RiskImportFile>
    {
        public void Configure(EntityTypeBuilder<RiskImportFile> builder)
        {
            builder.ToTable("RiskImportFiles");
            builder.HasKey(f => f.ImportId);
            builder.Property(f => f.ImportId).ValueGeneratedNever();
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<RiskImport>().WithOne().HasForeignKey<RiskImportFile>(f => f.ImportId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class RiskMeasureConfiguration : IEntityTypeConfiguration<RiskMeasure>
    {
        public void Configure(EntityTypeBuilder<RiskMeasure> builder)
        {
            builder.ToTable("RiskMeasures");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.HasOne<Requirement>().WithMany().HasForeignKey(m => m.RequirementId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(m => m.WorkItemId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }

    // US-111: "RiskActions" and their links in "RiskActionLinks"
    public class RiskActionConfiguration : IEntityTypeConfiguration<RiskAction>
    {
        public void Configure(EntityTypeBuilder<RiskAction> builder)
        {
            builder.ToTable("RiskActions");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Title).IsRequired().HasMaxLength(RiskAction.MaxTitleLength);
            builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(a => a.IsDone);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(a => a.DoneById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Links).WithOne().HasForeignKey(l => l.ActionId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Links).HasField("_links").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        }
    }

    public class RiskActionLinkConfiguration : IEntityTypeConfiguration<RiskActionLink>
    {
        public void Configure(EntityTypeBuilder<RiskActionLink> builder)
        {
            builder.ToTable("RiskActionLinks");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.HasOne<WorkItem>().WithMany().HasForeignKey(l => l.WorkItemId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Domain.Mappings.Evidence>().WithMany().HasForeignKey(l => l.EvidenceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
