using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How ApprovalRequest is stored in the table "ApprovalRequests" (US-135)
    public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
    {
        public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
        {
            builder.ToTable("ApprovalRequests");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(50);
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(r => r.SubjectStamp).HasMaxLength(50);
            builder.Property(r => r.Summary).IsRequired().HasMaxLength(ApprovalRequest.MaxSummaryLength);
            builder.Property(r => r.Payload).IsRequired().HasColumnType("jsonb");
            builder.Property(r => r.DecisionNote).HasMaxLength(ApprovalRequest.MaxReasonLength);
            builder.Ignore(r => r.IsPending);
            builder.Ignore(r => r.ExpiresAt);

            builder.HasOne<Organization>().WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).IsRequired(false).OnDelete(DeleteBehavior.Restrict); // US-137: null for an organisation's action
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(r => r.DecidedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => new { r.OrganizationId, r.Status });
            builder.HasIndex(r => r.ProjectId);
        }
    }
}
