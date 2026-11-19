using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-204 R5, R8: "documentation is enough" decisions in the table "DocumentationDecisions"; rows are only added
    public class DocumentationDecisionConfiguration : IEntityTypeConfiguration<DocumentationDecision>
    {
        public void Configure(EntityTypeBuilder<DocumentationDecision> builder)
        {
            builder.ToTable("DocumentationDecisions");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Reason).IsRequired().HasMaxLength(DocumentationDecision.MaxReasonLength);
            builder.Property(d => d.DecidedRole).HasConversion<string>().HasMaxLength(50);

            builder.HasOne<Organization>().WithMany().HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Requirement>().WithMany().HasForeignKey(d => d.RequirementId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.DecidedById).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(d => new { d.OrganizationId, d.RequirementId, d.DecidedAt });
        }
    }
}
