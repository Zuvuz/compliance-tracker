using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Feedback;
using FeedbackItem = ComplianceTracker.Domain.Feedback.Feedback;

namespace ComplianceTracker.Application.Feedback
{
    // Storage for feedback (implemented in Infrastructure). The screenshot's content is stored apart.
    public interface IFeedbackRepository
    {
        Task AddAsync(FeedbackItem feedback, byte[]? screenshot, CancellationToken cancellationToken = default);

        Task<int> CountSentSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FeedbackItem>> ListAllAsync(CancellationToken cancellationToken = default);

        Task<FeedbackItem?> GetAsync(Guid feedbackId, CancellationToken cancellationToken = default);

        Task<byte[]?> GetScreenshotAsync(Guid feedbackId, CancellationToken cancellationToken = default);
    }

    // R6: creates an issue in the tracker's own repository (implemented by the GitHub adapter)
    public interface IFeedbackIssueWriter
    {
        // Throws FeedbackIssueException when GitHub cannot be reached or the token is missing (R10)
        Task<CreatedIssue> CreateAsync(string title, string body, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);
    }

    public record CreatedIssue(int Number, string Url);

    public class FeedbackIssueException : Exception
    {
        public FeedbackIssueException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // US-063: sending feedback (any signed-in member), and creating a GitHub issue from it or dismissing it
    // (the system admin only, US-010). The sender's organisation logs all three.
    public class FeedbackService
    {
        // R9
        public const int MaxPerHour = 10;

        // R6
        public static readonly IReadOnlyList<string> IssueLabels = ["user-story", "status:draft", "feedback"];

        private readonly IFeedbackRepository _feedback;
        private readonly IFeedbackIssueWriter _issues;
        private readonly CurrentMember _member;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public FeedbackService(
            IFeedbackRepository feedback, IFeedbackIssueWriter issues, CurrentMember member, ICurrentUser currentUser,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _feedback = feedback;
            _issues = issues;
            _member = member;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1–R4, R9, R11
        public async Task<FeedbackItem> SendAsync(
            string? page, string asA, string iWant, string soThat, string? rules, string? examples,
            string? screenshotName, byte[]? screenshotContent, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var now = Now;

            if (await _feedback.CountSentSinceAsync(user.Id, now.AddHours(-1), cancellationToken) >= MaxPerHour)
            {
                throw new DomainException("You have sent a lot of feedback; try again later");
            }

            var screenshot = screenshotContent is { Length: > 0 } ? Screenshot.Create(screenshotName ?? "", screenshotContent) : null;
            var feedback = FeedbackItem.Send(organization.Id, user.Id, user.Role, now, page, asA, iWant, soThat, rules, examples, screenshot);

            await _feedback.AddAsync(feedback, screenshot?.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.FeedbackSent, feedback.Id, feedback.SuggestedIssueTitle(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return feedback;
        }

        // R6, R7, R10: the system admin may edit the title and the text first
        public async Task<CreatedIssue> CreateIssueAsync(Guid feedbackId, string title, string body, CancellationToken cancellationToken = default)
        {
            var feedback = await RequireAsync(feedbackId, cancellationToken);
            if (feedback.Status != FeedbackStatus.New)
            {
                throw new DomainException("The feedback has already been handled");
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            {
                throw new DomainException("Give the issue a title and a text");
            }

            CreatedIssue issue;
            try
            {
                issue = await _issues.CreateAsync(title.Trim(), body.Trim(), IssueLabels, cancellationToken);
            }
            catch (FeedbackIssueException e)
            {
                throw new DomainException(e.Message);
            }

            feedback.IssueCreated(issue.Number, issue.Url, AdminName, Now);
            await _log.LogSystemAsync(feedback.OrganizationId, ActivityAction.FeedbackIssueCreated, feedback.Id,
                $"System admin {AdminName}: issue #{issue.Number}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return issue;
        }

        // R8
        public async Task DismissAsync(Guid feedbackId, string reason, CancellationToken cancellationToken = default)
        {
            var feedback = await RequireAsync(feedbackId, cancellationToken);
            feedback.Dismiss(reason, AdminName, Now);
            await _log.LogSystemAsync(feedback.OrganizationId, ActivityAction.FeedbackDismissed, feedback.Id,
                $"System admin {AdminName}: {feedback.DismissReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5
        public async Task<IReadOnlyList<FeedbackItem>> ListAsync(CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            return await _feedback.ListAllAsync(cancellationToken);
        }

        public async Task<FeedbackItem?> GetAsync(Guid feedbackId, CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            return await _feedback.GetAsync(feedbackId, cancellationToken);
        }

        public async Task<(FeedbackItem Feedback, byte[] Content)?> ScreenshotAsync(Guid feedbackId, CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            var feedback = await _feedback.GetAsync(feedbackId, cancellationToken);
            var content = feedback is { HasScreenshot: true } ? await _feedback.GetScreenshotAsync(feedbackId, cancellationToken) : null;
            return content is null ? null : (feedback!, content);
        }

        private async Task<FeedbackItem> RequireAsync(Guid feedbackId, CancellationToken cancellationToken)
        {
            EnsureSystemAdmin();
            return await _feedback.GetAsync(feedbackId, cancellationToken) ?? throw new DomainException("Feedback not found");
        }

        private void EnsureSystemAdmin()
        {
            if (!_currentUser.IsSystemAdmin)
            {
                throw new DomainException("Only the system admin can do this");
            }
        }

        private string AdminName => _currentUser.Identity.Name;

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
