using ComplianceTracker.Application.Feedback;
using ComplianceTracker.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using FeedbackItem = ComplianceTracker.Domain.Feedback.Feedback;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IFeedbackRepository (US-063). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public FeedbackRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(FeedbackItem feedback, byte[]? screenshot, CancellationToken cancellationToken = default)
        {
            await _db.Feedback.AddAsync(feedback, cancellationToken);
            if (screenshot is not null)
            {
                await _db.FeedbackScreenshots.AddAsync(new FeedbackScreenshot { FeedbackId = feedback.Id, Content = screenshot }, cancellationToken);
            }
        }

        public Task<int> CountSentSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
            => _db.Feedback.CountAsync(f => f.UserId == userId && f.SentAt >= since, cancellationToken);

        public async Task<IReadOnlyList<FeedbackItem>> ListAllAsync(CancellationToken cancellationToken = default)
            => await _db.Feedback.OrderByDescending(f => f.SentAt).ToListAsync(cancellationToken);

        public Task<FeedbackItem?> GetAsync(Guid feedbackId, CancellationToken cancellationToken = default)
            => _db.Feedback.FirstOrDefaultAsync(f => f.Id == feedbackId, cancellationToken);

        public Task<byte[]?> GetScreenshotAsync(Guid feedbackId, CancellationToken cancellationToken = default)
            => _db.FeedbackScreenshots.Where(s => s.FeedbackId == feedbackId).Select(s => s.Content).FirstOrDefaultAsync(cancellationToken);
    }
}
