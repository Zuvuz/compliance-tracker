using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-192: "OrganisationChecklistItems"
    public class OrganisationChecklistItemConfiguration : IEntityTypeConfiguration<OrganisationChecklistItem>
    {
        public void Configure(EntityTypeBuilder<OrganisationChecklistItem> builder)
        {
            builder.ToTable("OrganisationChecklistItems");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.Phase).HasConversion<string>().HasMaxLength(50);
            builder.Property(i => i.Text).IsRequired().HasMaxLength(OrganisationChecklistItem.MaxTextLength);
            builder.Property(i => i.Source).IsRequired().HasMaxLength(200);
            builder.Property(i => i.Reference).IsRequired().HasMaxLength(OrganisationChecklistItem.MaxReferenceLength);
            builder.Ignore(i => i.IsActive);
            builder.Ignore(i => i.Key);

            builder.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(i => i.OrganizationId);
        }
    }
}
