using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How OrganizationFramework (a built-in framework switched on by an organisation) is stored
    // in the table "OrganizationFrameworks" (US-006). Rows are never deleted, so the history is kept.
    public class OrganizationFrameworkConfiguration : IEntityTypeConfiguration<OrganizationFramework>
    {
        public void Configure(EntityTypeBuilder<OrganizationFramework> builder)
        {
            builder.ToTable("OrganizationFrameworks");

            // The id is set by the domain, not by the database
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            // IsActive is calculated from DisabledAt and not stored
            builder.Ignore(s => s.IsActive);

            // Restrict everywhere: nothing an entry points at can be deleted while the history exists
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<FrameworkVersion>()
                .WithMany()
                .HasForeignKey(s => s.FrameworkVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.EnabledById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.DisabledById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up "is this framework switched on for this organisation?"
            builder.HasIndex(s => new { s.OrganizationId, s.FrameworkVersionId });
        }
    }
}
