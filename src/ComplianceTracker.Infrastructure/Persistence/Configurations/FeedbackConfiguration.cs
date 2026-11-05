using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackItem = ComplianceTracker.Domain.Feedback.Feedback;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Feedback is stored in the table "Feedback", and its screenshots in "FeedbackScreenshots" (US-063)
    public class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackItem>
    {
        public void Configure(EntityTypeBuilder<FeedbackItem> builder)
        {
            builder.ToTable("Feedback");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Role).HasConversion<string>().HasMaxLength(50);
            builder.Property(f => f.Page).IsRequired().HasMaxLength(FeedbackItem.MaxPageLength);
            builder.Property(f => f.AsA).IsRequired().HasMaxLength(FeedbackItem.MaxPartLength);
            builder.Property(f => f.IWant).IsRequired().HasMaxLength(FeedbackItem.MaxPartLength);
            builder.Property(f => f.SoThat).IsRequired().HasMaxLength(FeedbackItem.MaxPartLength);
            builder.Property(f => f.Rules).HasMaxLength(FeedbackItem.MaxOptionalLength);
            builder.Property(f => f.Examples).HasMaxLength(FeedbackItem.MaxOptionalLength);
            builder.Property(f => f.ScreenshotFileName).HasMaxLength(200);
            builder.Property(f => f.ScreenshotContentType).HasMaxLength(100);
            builder.Property(f => f.ScreenshotSha256).HasMaxLength(64);
            builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(f => f.IssueUrl).HasMaxLength(500);
            builder.Property(f => f.DismissReason).HasMaxLength(FeedbackItem.MaxPartLength);
            builder.Property(f => f.DecidedBy).HasMaxLength(200);
            builder.Ignore(f => f.HasScreenshot);

            builder.HasOne<Organization>().WithMany().HasForeignKey(f => f.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);

            // R9: counting a user's feedback in the last hour
            builder.HasIndex(f => new { f.UserId, f.SentAt });
        }
    }

    public class FeedbackScreenshotConfiguration : IEntityTypeConfiguration<FeedbackScreenshot>
    {
        public void Configure(EntityTypeBuilder<FeedbackScreenshot> builder)
        {
            builder.ToTable("FeedbackScreenshots");
            builder.HasKey(s => s.FeedbackId);
            builder.Property(s => s.Content).IsRequired();
            builder.HasOne<FeedbackItem>().WithOne().HasForeignKey<FeedbackScreenshot>(s => s.FeedbackId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    // A screenshot's bytes, apart from the feedback, so listing never loads them
    public class FeedbackScreenshot
    {
        public Guid FeedbackId { get; set; }
        public byte[] Content { get; set; } = [];
    }
}
