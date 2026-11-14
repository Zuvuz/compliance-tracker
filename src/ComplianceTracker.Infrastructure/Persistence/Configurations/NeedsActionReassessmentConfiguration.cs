using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How NeedsActionReassessment is stored in the table "NeedsActionReassessments" (US-156)
    public class NeedsActionReassessmentConfiguration : IEntityTypeConfiguration<NeedsActionReassessment>
    {
        public void Configure(EntityTypeBuilder<NeedsActionReassessment> builder)
        {
            builder.ToTable("NeedsActionReassessments");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Reasons).IsRequired().HasMaxLength(NeedsActionReassessment.MaxReasonsLength);
            builder.Property(r => r.Result).HasMaxLength(200);
            builder.Ignore(r => r.IsDone);

            builder.HasOne<Organization>().WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => new { r.ProjectId, r.DoneAt });
            builder.HasIndex(r => new { r.DoneAt, r.DueAt });
        }
    }
}
