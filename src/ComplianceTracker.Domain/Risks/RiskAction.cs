using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Risks
{
    // US-111: something the team does about a risk, with an owner, a due date, and the work items and evidence that
    // carry it out. Part of the Risk aggregate. It is done only with proof (R3), so a risk can be traced from the
    // assessment through the action to the proof (ISO 27001 6.1.3, 8.3; CRA Art. 13(2)).
    public class RiskAction
    {
        public const int MaxTitleLength = 200;

        private readonly List<RiskActionLink> _links = new();

        public Guid Id { get; } = Guid.NewGuid();
        public Guid RiskId { get; private set; }
        public string Title { get; private set; } = "";
        public Guid OwnerId { get; private set; }
        public DateOnly? DueOn { get; private set; }
        public RiskActionStatus Status { get; private set; } = RiskActionStatus.Planned;

        public Guid CreatedById { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public Guid? DoneById { get; private set; }
        public DateTime? DoneAt { get; private set; }

        public IReadOnlyList<RiskActionLink> Links => _links;

        public bool IsDone => Status == RiskActionStatus.Done;

        // R7: not done, and the due date has passed
        public bool IsOverdue(DateOnly today) => !IsDone && DueOn is { } due && due < today;

        // For EF Core only
        private RiskAction() { }

        internal RiskAction(Guid riskId, string title, Guid ownerId, DateOnly? dueOn, Guid userId, DateTime at)
        {
            var text = (title ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Give the action a title");
            }

            if (text.Length > MaxTitleLength)
            {
                throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
            }

            RiskId = riskId;
            Title = text;
            OwnerId = ownerId;
            DueOn = dueOn;
            CreatedById = userId;
            CreatedAt = at;
        }

        // R2: a work item or a piece of evidence of the project; the caller checks that it belongs to the project
        internal RiskActionLink Link(Guid? workItemId, Guid? evidenceId)
        {
            if (workItemId is null == evidenceId is null)
            {
                throw new DomainException("Choose a user story or evidence");
            }

            if (_links.Any(l => l.WorkItemId == workItemId && l.EvidenceId == evidenceId))
            {
                throw new DomainException("It is already linked to the action");
            }

            var link = new RiskActionLink(Id, workItemId, evidenceId);
            _links.Add(link);
            if (Status == RiskActionStatus.Planned)
            {
                Status = RiskActionStatus.InProgress;
            }

            return link;
        }

        // R3: hasProof says whether a linked work item is done or a linked evidence is there (the caller knows)
        internal void Complete(bool hasProof, Guid userId, DateTime at)
        {
            if (IsDone)
            {
                throw new DomainException("The action is already done");
            }

            if (!hasProof)
            {
                throw new DomainException("Link a done work item or evidence first");
            }

            Status = RiskActionStatus.Done;
            DoneById = userId;
            DoneAt = at;
        }
    }

    // R2: one work item or one piece of evidence that carries out the action
    public class RiskActionLink
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ActionId { get; private set; }
        public Guid? WorkItemId { get; private set; }
        public Guid? EvidenceId { get; private set; }

        // For EF Core only
        private RiskActionLink() { }

        internal RiskActionLink(Guid actionId, Guid? workItemId, Guid? evidenceId)
        {
            ActionId = actionId;
            WorkItemId = workItemId;
            EvidenceId = evidenceId;
        }
    }

    // Stored as text, like all enums
    public enum RiskActionStatus
    {
        Planned,
        InProgress,
        Done
    }
}
