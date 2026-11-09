using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-142 R3: how Nis2Assessment is stored in the table "Nis2Assessments". Rows are only added.
    public class Nis2AssessmentConfiguration : IEntityTypeConfiguration<Nis2Assessment>
    {
        public void Configure(EntityTypeBuilder<Nis2Assessment> builder)
        {
            builder.ToTable("Nis2Assessments");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Sector).HasConversion<string>().HasMaxLength(50);
            builder.Property(a => a.Service).HasConversion<string>().HasMaxLength(50);
            builder.Property(a => a.Result).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Reason).IsRequired().HasMaxLength(300);
            builder.Property(a => a.TurnoverMillions).HasPrecision(12, 2);
            builder.Property(a => a.BalanceSheetMillions).HasPrecision(12, 2);
            builder.Ignore(a => a.IsCovered);

            builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(a => new { a.OrganizationId, a.AssessedAt });
        }
    }
}
