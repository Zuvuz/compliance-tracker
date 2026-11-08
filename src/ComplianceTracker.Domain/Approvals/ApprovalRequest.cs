using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Approvals
{
    // US-135 R2: the actions that need a second person's approval (four-eyes)
    public enum ApprovalAction
    {
        AcceptRisk,
        RecordNotReportableEvent,
        EndSupport
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Expired
    }

    // US-135: an action that waits for another person's approval. The action only happens when it is approved (R2, R3).
    // What the action needs is kept as a small JSON payload, so the action is done exactly as it was asked for.
    public class ApprovalRequest
    {
        public static readonly TimeSpan ExpiresAfter = TimeSpan.FromDays(7); // R5
        public const int MaxSummaryLength = 500;
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid ProjectId { get; private set; }
        public ApprovalAction Action { get; private set; }

        // The risk or project the action changes; null when the action creates something new
        public Guid? SubjectId { get; private set; }

        // R4: the subject's version when the request was made; a changed subject cancels the request
        public string? SubjectStamp { get; private set; }

        // What will happen, in words, e.g. "Accept the risk "Stolen sessions" (High)"
        public string Summary { get; private set; } = "";
        public string Payload { get; private set; } = "{}";

        public Guid RequestedById { get; private set; }
        public DateTime RequestedAt { get; private set; }

        public ApprovalStatus Status { get; private set; }
        public Guid? DecidedById { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        // Why it was rejected or cancelled
        public string? DecisionNote { get; private set; }

        public DateTime ExpiresAt => RequestedAt + ExpiresAfter;

        public bool IsPending => Status == ApprovalStatus.Pending;

        // For EF Core only
        private ApprovalRequest() { }

        public static ApprovalRequest Create(
            Guid organizationId, Guid projectId, ApprovalAction action, Guid? subjectId, string? subjectStamp,
            string? summary, string? payload, Guid requestedById, DateTime now)
        {
            var text = (summary ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Say what will happen");
            }

            if (text.Length > MaxSummaryLength)
            {
                text = text[..(MaxSummaryLength - 1)] + "…";
            }

            return new ApprovalRequest
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Action = action,
                SubjectId = subjectId,
                SubjectStamp = subjectStamp,
                Summary = text,
                Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
                RequestedById = requestedById,
                RequestedAt = now,
                Status = ApprovalStatus.Pending
            };
        }

        // R5: still pending, but older than 7 days
        public bool IsDue(DateTime now) => IsPending && now >= ExpiresAt;

        // R3: only another person, and only while it is pending and not expired
        public void Approve(Guid approverId, DateTime now)
        {
            EnsureOpen(now);
            if (approverId == RequestedById)
            {
                throw new DomainException("Another person must approve this");
            }

            Decide(ApprovalStatus.Approved, approverId, null, now);
        }

        public void Reject(Guid approverId, string? reason, DateTime now)
        {
            EnsureOpen(now);
            if (approverId == RequestedById)
            {
                throw new DomainException("Withdraw your own request instead");
            }

            Decide(ApprovalStatus.Rejected, approverId, Note(reason, "Give a reason for rejecting"), now);
        }

        // The person who asked takes it back
        public void Withdraw(Guid userId, DateTime now)
        {
            if (!IsPending)
            {
                throw new DomainException("The request is decided already");
            }

            if (userId != RequestedById)
            {
                throw new DomainException("Only the person who asked can withdraw the request");
            }

            Decide(ApprovalStatus.Cancelled, userId, "Withdrawn", now);
        }

        // R4: the subject changed (or is gone) before it was approved
        public void Cancel(string reason, DateTime now)
        {
            if (!IsPending)
            {
                throw new DomainException("The request is decided already");
            }

            Decide(ApprovalStatus.Cancelled, null, Note(reason, "Give a reason"), now);
        }

        // R5: true when it expired now; false when it was not due
        public bool ExpireIfDue(DateTime now)
        {
            if (!IsDue(now))
            {
                return false;
            }

            Decide(ApprovalStatus.Expired, null, $"Not approved within {ExpiresAfter.Days} days", ExpiresAt);
            return true;
        }

        private void EnsureOpen(DateTime now)
        {
            if (!IsPending)
            {
                throw new DomainException("The request is decided already");
            }

            if (IsDue(now))
            {
                throw new DomainException($"The request expired after {ExpiresAfter.Days} days; ask again");
            }
        }

        private void Decide(ApprovalStatus status, Guid? userId, string? note, DateTime at)
        {
            Status = status;
            DecidedById = userId;
            DecidedAt = at;
            DecisionNote = note;
        }

        private static string Note(string? text, string missing)
        {
            var cleaned = (text ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException(missing);
            }

            return cleaned.Length > MaxReasonLength ? cleaned[..MaxReasonLength] : cleaned;
        }
    }
}
