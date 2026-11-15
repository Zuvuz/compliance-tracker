using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-189: how GapAnswer is stored in the table "GapAnswers". Rows are only added; a replaced one gets ReplacedAt.
    public class GapAnswerConfiguration : IEntityTypeConfiguration<GapAnswer>
    {
        public void Configure(EntityTypeBuilder<GapAnswer> builder)
        {
            builder.ToTable("GapAnswers");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.RequirementCode).IsRequired().HasMaxLength(GapAnswer.MaxCodeLength);
            builder.Property(a => a.Maturity).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.AnsweredRole).HasConversion<string>().HasMaxLength(50);
            builder.Property(a => a.Comment).HasMaxLength(GapAnswer.MaxCommentLength);
            builder.Ignore(a => a.IsCurrent);

            builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(a => new { a.OrganizationId, a.RequirementCode, a.AnsweredAt });
        }
    }
}
