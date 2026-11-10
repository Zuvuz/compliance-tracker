using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-136 R2: how DocumentLinkSuggestion is stored in the table "DocumentLinkSuggestions"
    public class DocumentLinkSuggestionConfiguration : IEntityTypeConfiguration<DocumentLinkSuggestion>
    {
        public void Configure(EntityTypeBuilder<DocumentLinkSuggestion> builder)
        {
            builder.ToTable("DocumentLinkSuggestions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.RequirementCode).IsRequired().HasMaxLength(DocumentLinkSuggestion.MaxCodeLength);
            builder.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(s => s.DecidedRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.RejectionReason).HasConversion<string>().HasMaxLength(50);
            builder.Property(s => s.Comment).HasMaxLength(1000);
            builder.Ignore(s => s.IsPending);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<OrganisationDocument>().WithMany().HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Domain.Frameworks.Framework>().WithMany().HasForeignKey(s => s.FrameworkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(s => new { s.OrganizationId, s.DocumentId });
        }
    }
}
