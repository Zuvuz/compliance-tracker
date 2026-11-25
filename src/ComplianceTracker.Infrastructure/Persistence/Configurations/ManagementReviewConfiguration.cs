using System.Text.Json;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-210: the management reviews in "ManagementReviews" (participants as a text array, the input snapshot as JSON) and
    // their decisions in "ReviewDecisions"
    public class ManagementReviewConfiguration : IEntityTypeConfiguration<ManagementReview>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<ManagementReview> builder)
        {
            builder.ToTable("ManagementReviews");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.Feedback).HasMaxLength(ManagementReview.MaxTextLength);
            builder.Property(r => r.Improvements).HasMaxLength(ManagementReview.MaxTextLength);
            builder.Property(r => r.Inputs)
                .HasColumnType("jsonb")
                .HasConversion(
                    list => JsonSerializer.Serialize(list, Json),
                    json => JsonSerializer.Deserialize<List<ReviewInput>>(json, Json) ?? new List<ReviewInput>(),
                    new ValueComparer<List<ReviewInput>>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c.ToList()));
            builder.HasOne<Organization>().WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(r => r.OrganizationId);
            builder.HasMany(r => r.Decisions).WithOne().HasForeignKey(d => d.ReviewId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
    {
        public void Configure(EntityTypeBuilder<ReviewDecision> builder)
        {
            builder.ToTable("ReviewDecisions");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();
            builder.Property(d => d.Text).IsRequired().HasMaxLength(ManagementReview.MaxTextLength);
            builder.Ignore(d => d.HasAction);
            builder.Ignore(d => d.IsOpen);
            builder.HasIndex(d => d.ReviewId);
        }
    }
}
