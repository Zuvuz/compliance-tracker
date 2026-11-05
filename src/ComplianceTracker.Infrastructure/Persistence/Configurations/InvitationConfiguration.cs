using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Invitation is stored in the table "Invitations" (US-009). Only the token's hash is stored (R6).
    public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
    {
        public void Configure(EntityTypeBuilder<Invitation> builder)
        {
            builder.ToTable("Invitations");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(50);
            builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64);
            builder.HasIndex(i => i.TokenHash).IsUnique();

            builder.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
