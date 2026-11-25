using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    public enum ReviewStatus
    {
        Planned,
        Held,
        Locked
    }

    // US-210 R2: one part of the input pack (ISO/IEC 27001 9.3.2), as the management saw it
    public sealed record ReviewInput(string Clause, string Title, IReadOnlyList<string> Lines)
    {
        public const string NothingToReport = "Nothing to report";
    }

    // US-210: the management review of the ISMS (ISO/IEC 27001 9.3): planned with participants, held with the inputs kept as
    // a snapshot, its decisions with actions, and locked as minutes.
    public class ManagementReview
    {
        public const int MaxTextLength = 2000;
        public const int MaxParticipants = 30;

        // R6: a review is due every 12 months
        public const int IntervalMonths = 12;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public DateOnly PlannedOn { get; private set; }
        public List<string> Participants { get; private set; } = [];
        public ReviewStatus Status { get; private set; }
        public DateOnly? HeldOn { get; private set; }

        // e) and g): written by hand before the review is held
        public string? Feedback { get; private set; }
        public string? Improvements { get; private set; }

        // R3: the inputs as they were when the review was held
        public List<ReviewInput> Inputs { get; private set; } = [];

        public DateTime? LockedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private readonly List<ReviewDecision> _decisions = [];
        public IReadOnlyList<ReviewDecision> Decisions => _decisions.OrderBy(d => d.At).ToList();

        // For EF Core only
        private ManagementReview() { }

        // R1
        public static ManagementReview Plan(Guid organizationId, DateOnly? plannedOn, IEnumerable<string?>? participants, DateTime at)
        {
            var review = new ManagementReview { OrganizationId = organizationId, Status = ReviewStatus.Planned, CreatedAt = at };
            review.Change(plannedOn, participants);
            return review;
        }

        public void Change(DateOnly? plannedOn, IEnumerable<string?>? participants)
        {
            RequireStatus(ReviewStatus.Planned, "The review has been held; its date and participants are kept");
            if (plannedOn is null)
            {
                throw new DomainException("Give the review a date");
            }

            var clean = (participants ?? []).Select(p => (p ?? "").Trim()).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (clean.Count == 0)
            {
                throw new DomainException("Name at least one participant");
            }

            if (clean.Count > MaxParticipants || clean.Any(p => p.Length > 200))
            {
                throw new DomainException($"At most {MaxParticipants} participants, each 200 characters or fewer");
            }

            PlannedOn = plannedOn.Value;
            Participants = clean;
        }

        // e) and g)
        public void Describe(string? feedback, string? improvements)
        {
            RequireStatus(ReviewStatus.Planned, "The review has been held; its inputs are kept as they were");
            Feedback = Text(feedback, "The feedback");
            Improvements = Text(improvements, "The opportunities");
        }

        // R2, R3: held on a day; the inputs are kept as they were then
        public void Hold(DateOnly? heldOn, IEnumerable<ReviewInput> inputs, DateOnly today)
        {
            RequireStatus(ReviewStatus.Planned, "The review has been held already");
            if (heldOn is null)
            {
                throw new DomainException("Give the day the review was held");
            }

            if (heldOn > today)
            {
                throw new DomainException("A review cannot be held in the future");
            }

            HeldOn = heldOn;
            Inputs = inputs.Select(i => i.Lines.Count == 0 ? i with { Lines = [ReviewInput.NothingToReport] } : i).ToList();
            Status = ReviewStatus.Held;
        }

        // R4
        public ReviewDecision Decide(string? text, Guid? actionOwnerId, DateOnly? actionDueOn, DateTime at)
        {
            RequireStatus(ReviewStatus.Held, Status == ReviewStatus.Locked ? "The minutes are locked" : "Hold the review before recording decisions");
            var clean = Text(text, "The decision") ?? throw new DomainException("Write the decision");
            if ((actionOwnerId is null) != (actionDueOn is null))
            {
                throw new DomainException("An action needs both an owner and a deadline");
            }

            var decision = new ReviewDecision(Id, clean, actionOwnerId, actionDueOn, at);
            _decisions.Add(decision);
            return decision;
        }

        // R5: the minutes cannot be changed after this
        public void Lock(DateTime at)
        {
            RequireStatus(ReviewStatus.Held, Status == ReviewStatus.Locked ? "The minutes are locked already" : "Hold the review before locking the minutes");
            Status = ReviewStatus.Locked;
            LockedAt = at;
        }

        // R5: an action can be done also after the minutes are locked
        public ReviewDecision MarkActionDone(Guid decisionId, DateTime at)
        {
            var decision = _decisions.FirstOrDefault(d => d.Id == decisionId) ?? throw new DomainException("Decision not found");
            decision.Done(at);
            return decision;
        }

        public bool IsOverdue(DateOnly today) => Status == ReviewStatus.Planned && PlannedOn < today;

        // R6: no review held for 12 months, or never
        public static bool ReviewIsDue(IEnumerable<ManagementReview> reviews, DateOnly today)
            => reviews.Where(r => r.HeldOn is not null).Max(r => r.HeldOn) is not { } last || last.AddMonths(IntervalMonths) <= today;

        private void RequireStatus(ReviewStatus status, string message)
        {
            if (Status != status)
            {
                throw new DomainException(message);
            }
        }

        private static string? Text(string? value, string what)
        {
            var clean = (value ?? "").Trim();
            if (clean.Length > MaxTextLength)
            {
                throw new DomainException($"{what} must be {MaxTextLength} characters or fewer");
            }

            return clean.Length == 0 ? null : clean;
        }
    }

    // US-210 R4: a decision of the review, with an action for an owner by a deadline when there is one
    public class ReviewDecision
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ReviewId { get; private set; }
        public string Text { get; private set; } = "";
        public Guid? ActionOwnerId { get; private set; }
        public DateOnly? ActionDueOn { get; private set; }
        public DateTime? ActionDoneAt { get; private set; }
        public DateTime At { get; private set; }

        public bool HasAction => ActionOwnerId is not null;
        public bool IsOpen => HasAction && ActionDoneAt is null;

        // For EF Core only
        private ReviewDecision() { }

        internal ReviewDecision(Guid reviewId, string text, Guid? ownerId, DateOnly? dueOn, DateTime at)
        {
            ReviewId = reviewId;
            Text = text;
            ActionOwnerId = ownerId;
            ActionDueOn = dueOn;
            At = at;
        }

        internal void Done(DateTime at)
        {
            if (!HasAction)
            {
                throw new DomainException("The decision has no action");
            }

            if (ActionDoneAt is not null)
            {
                throw new DomainException("The action is done already");
            }

            ActionDoneAt = at;
        }
    }
}
