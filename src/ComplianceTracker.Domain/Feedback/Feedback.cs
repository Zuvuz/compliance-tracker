using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System.Globalization;
using System.Text;

namespace ComplianceTracker.Domain.Feedback
{
    // US-063: an idea or a problem a member sent from the footer, shaped like a user story. Its own aggregate.
    // Only the system admin sees it (R5). It is never deleted: creating an issue or dismissing it sets the status.
    // The screenshot's content is stored apart, like evidence files (US-057).
    public class Feedback
    {
        public const int MaxPartLength = 1000;
        public const int MaxOptionalLength = 4000;
        public const int MaxPageLength = 500;

        public Guid Id { get; } = Guid.NewGuid();

        public Guid OrganizationId { get; private set; }
        public Guid UserId { get; private set; }

        // US-010: the role when it was sent
        public UserRole? Role { get; private set; }
        public DateTime SentAt { get; private set; }

        // R1: the page it was sent from, e.g. "/Projects/Details/…/compliance/cra" (a path in the tracker only)
        public string Page { get; private set; } = "/";

        // R2: "As a …, I want …, so that …", and optional rules and examples
        public string AsA { get; private set; } = "";
        public string IWant { get; private set; } = "";
        public string SoThat { get; private set; } = "";
        public string? Rules { get; private set; }
        public string? Examples { get; private set; }

        // R3: the optional screenshot
        public string? ScreenshotFileName { get; private set; }
        public string? ScreenshotContentType { get; private set; }
        public long? ScreenshotSizeInBytes { get; private set; }
        public string? ScreenshotSha256 { get; private set; }

        public FeedbackStatus Status { get; private set; } = FeedbackStatus.New;

        // R6, R7: the GitHub issue made from it
        public int? IssueNumber { get; private set; }
        public string? IssueUrl { get; private set; }

        // R8
        public string? DismissReason { get; private set; }

        // The system admin is not a member of the organisation, so their name is kept, as in the log (US-010)
        public string? DecidedBy { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        public bool HasScreenshot => ScreenshotSha256 is not null;

        // For EF Core only
        private Feedback() { }

        public static Feedback Send(
            Guid organizationId, Guid userId, UserRole? role, DateTime at, string? page,
            string asA, string iWant, string soThat, string? rules, string? examples, Screenshot? screenshot)
        {
            var feedback = new Feedback
            {
                OrganizationId = organizationId,
                UserId = userId,
                Role = role,
                SentAt = at,
                Page = LocalPath(page),
                AsA = Required(asA, "As a"),
                IWant = Required(iWant, "I want"),
                SoThat = Required(soThat, "so that"),
                Rules = Optional(rules, "Rules"),
                Examples = Optional(examples, "Examples")
            };

            if (screenshot is not null)
            {
                feedback.ScreenshotFileName = screenshot.FileName;
                feedback.ScreenshotContentType = screenshot.ContentType;
                feedback.ScreenshotSizeInBytes = screenshot.Content.Length;
                feedback.ScreenshotSha256 = screenshot.Sha256;
            }

            return feedback;
        }

        // R6: a title for the issue, from "I want …", e.g. "Filter the risks by owner"
        public string SuggestedIssueTitle()
        {
            var want = IWant.Trim();
            if (want.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
            {
                want = want[3..];
            }

            want = want.Length > 100 ? want[..100].TrimEnd() + "…" : want;
            return want.Length == 0 ? "Feedback" : char.ToUpperInvariant(want[0]) + want[1..];
        }

        // R6: the user story template's headings. Never the sender, the organisation or the screenshot itself.
        public string SuggestedIssueBody()
        {
            var body = new StringBuilder();
            body.AppendLine($"From user feedback, {SentAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}.");
            body.AppendLine();
            body.AppendLine("### Role").AppendLine().AppendLine(AsA).AppendLine();
            body.AppendLine("### User Story").AppendLine().AppendLine($"As a {AsA}, I want {IWant}, so that {SoThat}.").AppendLine();
            body.AppendLine("### Rules").AppendLine().AppendLine(Rules ?? "_None given._").AppendLine();
            body.AppendLine("### Examples").AppendLine().AppendLine(Examples ?? "_None given._").AppendLine();
            body.Append(HasScreenshot ? "A screenshot is attached in the tracker." : "No screenshot was sent.");
            return body.ToString();
        }

        public void IssueCreated(int number, string url, string decidedBy, DateTime at)
        {
            EnsureNew();
            Status = FeedbackStatus.IssueCreated;
            IssueNumber = number;
            IssueUrl = url;
            DecidedBy = decidedBy;
            DecidedAt = at;
        }

        public void Dismiss(string reason, string decidedBy, DateTime at)
        {
            EnsureNew();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for dismissing the feedback");
            }

            Status = FeedbackStatus.Dismissed;
            DismissReason = Truncated(reason.Trim(), MaxPartLength);
            DecidedBy = decidedBy;
            DecidedAt = at;
        }

        private void EnsureNew()
        {
            if (Status != FeedbackStatus.New)
            {
                throw new DomainException("The feedback has already been handled");
            }
        }

        private static string Required(string? value, string field)
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException($"Fill in \"{field}\"");
            }

            if (text.Length > MaxPartLength)
            {
                throw new DomainException($"\"{field}\" must be {MaxPartLength} characters or fewer");
            }

            return text;
        }

        private static string? Optional(string? value, string field)
        {
            var text = (value ?? "").Trim();
            if (text.Length > MaxOptionalLength)
            {
                throw new DomainException($"\"{field}\" must be {MaxOptionalLength} characters or fewer");
            }

            return text.Length == 0 ? null : text;
        }

        // Only a path inside the tracker, so the "back" link can never send the user to another site
        private static string LocalPath(string? page)
        {
            var path = (page ?? "").Trim();
            return path.StartsWith('/') && !path.StartsWith("//") && !path.StartsWith("/\\") && path.Length <= MaxPageLength
                ? path
                : "/";
        }

        private static string Truncated(string text, int max) => text.Length > max ? text[..max] : text;
    }

    public enum FeedbackStatus
    {
        New,
        IssueCreated,
        Dismissed
    }
}
