using ComplianceTracker.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How AiEvaluation is stored in the table "AiEvaluations" (US-116). Rows are only added.
    public class AiEvaluationConfiguration : IEntityTypeConfiguration<AiEvaluation>
    {
        public void Configure(EntityTypeBuilder<AiEvaluation> builder)
        {
            builder.ToTable("AiEvaluations");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Step).HasConversion<string>().HasMaxLength(30);
            builder.Property(e => e.Model).IsRequired().HasMaxLength(200);
            builder.Property(e => e.PromptVersion).IsRequired().HasMaxLength(50);
            builder.Property(e => e.RunBy).IsRequired().HasMaxLength(200);
            builder.HasIndex(e => new { e.Step, e.RunAt });
        }
    }
}
