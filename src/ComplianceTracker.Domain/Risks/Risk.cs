using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Risks
{
    // A cybersecurity risk of a project's product (US-042): the CRA's risk assessment (Art. 13(2), Annex VII 3)
    // and ISO 27001's risk assessment and treatment (6.1.2, 6.1.3, 8.2).
    // Its own aggregate, like WorkItem. A risk is never deleted; one that no longer applies is closed with a reason (R7).
    public class Risk
    {
        public const int MinScale = 1;
        public const int MaxScale = 5;

        // R6: a risk not reviewed for this long is "Review due"
        public const int ReviewMonths = 12;

        private readonly List<RiskMeasure> _measures = new();

        public Guid Id { get; } = Guid.NewGuid();

        // US-092: the row's version, set by the database (PostgreSQL's xmin). A form carries it, so a save
        // can tell that someone else changed the row after the form was opened.
        public uint Version { get; private set; }
        public Guid ProjectId { get; private set; }

        public string Title { get; private set; } = "";

        // The threat and what it affects (optional)
        public string Description { get; private set; } = "";

        // R1: 1 (rare / minor) to 5 (almost certain / severe)
        public int Likelihood { get; private set; }
        public int Impact { get; private set; }

        // The risk left after the measures (optional, both or none)
        public int? ResidualLikelihood { get; private set; }
        public int? ResidualImpact { get; private set; }

        // R3: a member of the organisation
        public Guid OwnerId { get; private set; }
        public RiskTreatment Treatment { get; private set; }

        public RiskStatus Status { get; private set; } = RiskStatus.Open;

        // R4: why the risk is accepted, and who accepted it
        public string? AcceptedReason { get; private set; }
        public Guid? AcceptedById { get; private set; }

        // US-010: the role of the person when they accepted it
        public UserRole? AcceptedRole { get; private set; }
        public DateTime? AcceptedAt { get; private set; }

        // R7: why the risk was closed
        public string? ClosedReason { get; private set; }
        public DateTime? ClosedAt { get; private set; }

        public Guid CreatedById { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // R6: when it was last reviewed, and by whom; null until the first review
        public DateTime? LastReviewedAt { get; private set; }
        public Guid? LastReviewedById { get; private set; }

        // R5: the project's requirements and user stories that reduce the risk
        public IReadOnlyList<RiskMeasure> Measures => _measures;

        // R2: calculated, never typed
        public int Score => Likelihood * Impact;
        public RiskLevel Level => LevelOf(Score);
        public RiskLevel? ResidualLevel => ResidualLikelihood is { } l && ResidualImpact is { } i ? LevelOf(l * i) : null;

        // R5: a risk to reduce with nothing linked yet
        public bool HasNoMeasures => Status == RiskStatus.Open && Treatment == RiskTreatment.Reduce && _measures.Count == 0;

        // What the register shows: "Treated" for a risk to reduce that has measures
        public string DisplayStatus => Status == RiskStatus.Open && Treatment == RiskTreatment.Reduce && _measures.Count > 0
            ? "Treated"
            : Status.ToString();

        // For EF Core only
        private Risk() { }

        public static Risk Create(
            Guid projectId, string title, string? description, int likelihood, int impact,
            Guid ownerId, RiskTreatment treatment, Guid userId, DateTime at)
        {
            var risk = new Risk { ProjectId = projectId, CreatedById = userId, CreatedAt = at };
            risk.Set(title, description, likelihood, impact, ownerId, treatment);
            return risk;
        }

        // R1, R3, R9: returns what changed, for the log (e.g. "Likelihood: 3 → 2")
        public IReadOnlyList<ProjectChange> Update(
            string title, string? description, int likelihood, int impact, Guid ownerId, RiskTreatment treatment,
            int? residualLikelihood, int? residualImpact)
        {
            EnsureNotClosed();
            var before = Snapshot();
            Set(title, description, likelihood, impact, ownerId, treatment);
            SetResidual(residualLikelihood, residualImpact);

            // Choosing another treatment than Accept ends an earlier acceptance
            if (Status == RiskStatus.Accepted && treatment != RiskTreatment.Accept)
            {
                Status = RiskStatus.Open;
            }

            return Diff(before, Snapshot());
        }

        // R4: accepting needs a reason; the person and time are kept
        public ProjectChange Accept(string? reason, Guid userId, DateTime at, UserRole? role = null)
        {
            EnsureNotClosed();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for accepting the risk");
            }

            var change = new ProjectChange("Status", DisplayStatus, $"Accepted ({reason.Trim()})");
            Treatment = RiskTreatment.Accept;
            Status = RiskStatus.Accepted;
            AcceptedReason = reason.Trim();
            AcceptedById = userId;
            AcceptedRole = role;
            AcceptedAt = at;
            return change;
        }

        // R7: closing needs a reason; the risk stays in the register
        public ProjectChange Close(string? reason, DateTime at)
        {
            EnsureNotClosed();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for closing the risk");
            }

            var change = new ProjectChange("Status", DisplayStatus, $"Closed ({reason.Trim()})");
            Status = RiskStatus.Closed;
            ClosedReason = reason.Trim();
            ClosedAt = at;
            return change;
        }

        // R6
        public void MarkReviewed(Guid userId, DateTime at)
        {
            EnsureNotClosed();
            LastReviewedAt = at;
            LastReviewedById = userId;
        }

        // R6: not reviewed (or created) within the last 12 months; closed risks are never due
        public bool IsReviewDue(DateTime now)
            => Status != RiskStatus.Closed && (LastReviewedAt ?? CreatedAt).AddMonths(ReviewMonths) < now;

        // R5: the caller checks that the requirement or story belongs to the project
        public RiskMeasure AddMeasure(Guid? requirementId, Guid? workItemId)
        {
            EnsureNotClosed();
            if (requirementId is null == workItemId is null)
            {
                throw new DomainException("Choose a requirement or a user story");
            }

            if (_measures.Any(m => m.RequirementId == requirementId && m.WorkItemId == workItemId))
            {
                throw new DomainException("The measure is already linked to the risk");
            }

            var measure = new RiskMeasure(Id, requirementId, workItemId);
            _measures.Add(measure);
            return measure;
        }

        public void RemoveMeasure(Guid measureId)
        {
            EnsureNotClosed();
            var measure = _measures.FirstOrDefault(m => m.Id == measureId)
                ?? throw new DomainException("Measure not found");
            _measures.Remove(measure);
        }

        public static RiskLevel LevelOf(int score) => score switch
        {
            <= 4 => RiskLevel.Low,
            <= 9 => RiskLevel.Medium,
            <= 16 => RiskLevel.High,
            _ => RiskLevel.Critical
        };

        private void Set(string title, string? description, int likelihood, int impact, Guid ownerId, RiskTreatment treatment)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new DomainException("Risk title is required");
            }

            EnsureScale(likelihood, "Likelihood");
            EnsureScale(impact, "Impact");
            if (!Enum.IsDefined(treatment))
            {
                throw new DomainException("Choose a treatment");
            }

            Title = title.Trim();
            Description = description?.Trim() ?? "";
            Likelihood = likelihood;
            Impact = impact;
            OwnerId = ownerId;
            Treatment = treatment;
        }

        private void SetResidual(int? likelihood, int? impact)
        {
            if (likelihood is null != impact is null)
            {
                throw new DomainException("Give both the residual likelihood and impact, or neither");
            }

            if (likelihood is { } l && impact is { } i)
            {
                EnsureScale(l, "Residual likelihood");
                EnsureScale(i, "Residual impact");
            }

            ResidualLikelihood = likelihood;
            ResidualImpact = impact;
        }

        private static void EnsureScale(int value, string name)
        {
            if (value < MinScale || value > MaxScale)
            {
                throw new DomainException($"{name} must be from {MinScale} to {MaxScale}");
            }
        }

        private void EnsureNotClosed()
        {
            if (Status == RiskStatus.Closed)
            {
                throw new DomainException("A closed risk cannot be changed");
            }
        }

        private Dictionary<string, string> Snapshot() => new()
        {
            ["Title"] = Title,
            ["Description"] = Description,
            ["Likelihood"] = Likelihood.ToString(),
            ["Impact"] = Impact.ToString(),
            ["Owner"] = OwnerId.ToString(),
            ["Treatment"] = Treatment.ToString(),
            ["Residual likelihood"] = ResidualLikelihood?.ToString() ?? "",
            ["Residual impact"] = ResidualImpact?.ToString() ?? ""
        };

        private static IReadOnlyList<ProjectChange> Diff(Dictionary<string, string> before, Dictionary<string, string> after)
            => before.Where(b => b.Value != after[b.Key]).Select(b => new ProjectChange(b.Key, b.Value, after[b.Key])).ToList();
    }

    // R5: one requirement or one user story that reduces the risk
    public class RiskMeasure
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid RiskId { get; private set; }
        public Guid? RequirementId { get; private set; }
        public Guid? WorkItemId { get; private set; }

        // For EF Core only
        private RiskMeasure() { }

        internal RiskMeasure(Guid riskId, Guid? requirementId, Guid? workItemId)
        {
            RiskId = riskId;
            RequirementId = requirementId;
            WorkItemId = workItemId;
        }
    }

    // Stored as text, like all enums
    public enum RiskTreatment
    {
        Reduce,
        Accept,
        Avoid,
        Transfer
    }

    public enum RiskStatus
    {
        Open,
        Accepted,
        Closed
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }
}
