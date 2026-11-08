using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How the organisation's documents are stored (US-136): "OrganisationDocuments" with their links and earlier versions,
    // and the files in "OrganisationDocumentFiles"
    public class OrganisationDocumentConfiguration : IEntityTypeConfiguration<OrganisationDocument>
    {
        public void Configure(EntityTypeBuilder<OrganisationDocument> builder)
        {
            builder.ToTable("OrganisationDocuments");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(50);
            builder.Property(d => d.Title).IsRequired().HasMaxLength(OrganisationDocument.MaxTitleLength);
            builder.Property(d => d.Version).IsRequired().HasMaxLength(OrganisationDocument.MaxVersionLength);
            builder.Property(d => d.FileName).IsRequired().HasMaxLength(200);
            builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(d => d.Sha256).IsRequired().HasMaxLength(64);
            builder.Ignore(d => d.IsActive);
            builder.Ignore(d => d.ActiveLinks);

            builder.HasOne<Organization>().WithMany().HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.AddedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(d => d.LastReviewedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(d => d.Links).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(d => d.Links).HasField("_links").UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
            builder.Navigation(d => d.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(d => d.OrganizationId);
        }
    }

    public class OrganisationDocumentLinkConfiguration : IEntityTypeConfiguration<OrganisationDocumentLink>
    {
        public void Configure(EntityTypeBuilder<OrganisationDocumentLink> builder)
        {
            builder.ToTable("OrganisationDocumentLinks");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.RequirementCode).IsRequired().HasMaxLength(50);
            builder.Ignore(l => l.IsActive);
            builder.HasOne<Domain.Frameworks.Framework>().WithMany().HasForeignKey(l => l.FrameworkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.LinkedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(l => l.RemovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class OrganisationDocumentVersionConfiguration : IEntityTypeConfiguration<OrganisationDocumentVersion>
    {
        public void Configure(EntityTypeBuilder<OrganisationDocumentVersion> builder)
        {
            builder.ToTable("OrganisationDocumentVersions");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();
            builder.Property(v => v.Version).IsRequired().HasMaxLength(OrganisationDocument.MaxVersionLength);
            builder.Property(v => v.FileName).IsRequired().HasMaxLength(200);
            builder.Property(v => v.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64);
            builder.HasOne<User>().WithMany().HasForeignKey(v => v.ReplacedById).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class OrganisationDocumentFileConfiguration : IEntityTypeConfiguration<OrganisationDocumentFile>
    {
        public void Configure(EntityTypeBuilder<OrganisationDocumentFile> builder)
        {
            builder.ToTable("OrganisationDocumentFiles");
            builder.HasKey(f => new { f.DocumentId, f.Sha256 });
            builder.Property(f => f.Sha256).HasMaxLength(64);
            builder.Property(f => f.Content).IsRequired();
            builder.HasOne<OrganisationDocument>().WithMany().HasForeignKey(f => f.DocumentId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
