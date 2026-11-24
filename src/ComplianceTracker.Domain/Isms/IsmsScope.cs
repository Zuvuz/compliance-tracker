using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    // Whether a project is inside the ISMS scope (US-208 R2, R3). A project nobody has decided on yet is outside.
    public enum ScopeState
    {
        Inside,
        Outside,
        NotDecided
    }

    // US-208: the scope and context of the organisation's information security management system (ISO/IEC 27001 4.1, 4.3,
    // 4.4). One per organisation (R1). It has a version that goes up each time it is approved, an owner and a review date.
    public class IsmsScope
    {
        public const int MaxTextLength = 4000;
        public const int MaxShortTextLength = 1000;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // 4.3: what the ISMS covers, in the organisation's own words
        public string Text { get; private set; } = "";
        public string? Locations { get; private set; }
        public string? Departments { get; private set; }

        // 4.1: the internal and external issues that matter for information security
        public string? InternalIssues { get; private set; }
        public string? ExternalIssues { get; private set; }

        public Guid? OwnerId { get; private set; }
        public DateOnly? NextReviewOn { get; private set; }

        // 0 until the scope is approved the first time; each approval makes a new version (4.4)
        public int Version { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public Guid? ApprovedById { get; private set; }
        public DateTime ChangedAt { get; private set; }

        // Changed after the last approval, so the approved version is not what the page shows. A flag, not a comparison of
        // times, so a change in the same second as the approval still counts.
        public bool ChangedSinceApproval { get; private set; } = true;

        private readonly List<ScopedProject> _projects = [];
        public IReadOnlyList<ScopedProject> Projects => _projects;

        // For EF Core only
        private IsmsScope() { }

        public static IsmsScope Create(Guid organizationId, DateTime at)
            => new() { OrganizationId = organizationId, ChangedAt = at };

        // R1: describes the scope; returns the names of the fields that changed, for the log
        public IReadOnlyList<string> Describe(string? text, string? locations, string? departments, string? internalIssues, string? externalIssues,
            Guid? ownerId, DateOnly? nextReviewOn, DateTime at)
        {
            var cleanText = Clean(text, MaxTextLength, "The scope") ?? "";
            var changes = new List<string>();
            void Set<T>(string name, T old, T value, Action<T> apply)
            {
                if (!EqualityComparer<T>.Default.Equals(old, value))
                {
                    changes.Add(name);
                    apply(value);
                }
            }

            Set("Scope", Text, cleanText, v => Text = v);
            Set("Locations", Locations, Clean(locations, MaxShortTextLength, "The locations"), v => Locations = v);
            Set("Departments", Departments, Clean(departments, MaxShortTextLength, "The departments"), v => Departments = v);
            Set("Internal issues", InternalIssues, Clean(internalIssues, MaxTextLength, "The internal issues"), v => InternalIssues = v);
            Set("External issues", ExternalIssues, Clean(externalIssues, MaxTextLength, "The external issues"), v => ExternalIssues = v);
            Set("Owner", OwnerId, ownerId, v => OwnerId = v);
            Set("Next review", NextReviewOn, nextReviewOn, v => NextReviewOn = v);
            if (changes.Count > 0)
            {
                Changed(at);
            }

            return changes;
        }

        // R2: a project inside the scope
        public bool Include(Guid projectId, DateTime at)
        {
            var existing = _projects.FirstOrDefault(p => p.ProjectId == projectId);
            if (existing is { IsInside: true })
            {
                return false;
            }

            if (existing is null)
            {
                _projects.Add(ScopedProject.Inside(Id, projectId));
            }
            else
            {
                existing.MoveInside();
            }

            Changed(at);
            return true;
        }

        // R2: a project outside the scope needs a reason
        public bool Exclude(Guid projectId, string? reason, DateTime at)
        {
            var clean = Clean(reason, MaxShortTextLength, "The reason");
            if (clean is null)
            {
                throw new DomainException("Give the reason the project is outside the scope");
            }

            var existing = _projects.FirstOrDefault(p => p.ProjectId == projectId);
            if (existing is { IsInside: false } && existing.Reason == clean)
            {
                return false;
            }

            if (existing is null)
            {
                _projects.Add(ScopedProject.Outside(Id, projectId, clean));
            }
            else
            {
                existing.MoveOutside(clean);
            }

            Changed(at);
            return true;
        }

        // R3: a project nobody has decided on is outside until it is put inside
        public ScopeState StateOf(Guid projectId)
            => _projects.FirstOrDefault(p => p.ProjectId == projectId) switch
            {
                null => ScopeState.NotDecided,
                { IsInside: true } => ScopeState.Inside,
                _ => ScopeState.Outside
            };

        public bool IsInside(Guid projectId) => StateOf(projectId) == ScopeState.Inside;

        // 4.4: approving makes a new version; there must be a scope text and at least one project inside
        public int Approve(Guid userId, DateTime at)
        {
            if (Text.Length == 0)
            {
                throw new DomainException("Describe the scope before it is approved");
            }

            if (!_projects.Any(p => p.IsInside))
            {
                throw new DomainException("Put at least one project inside the scope before it is approved");
            }

            if (!ChangedSinceApproval)
            {
                throw new DomainException("This version of the scope is approved already");
            }

            Version++;
            ApprovedAt = at;
            ApprovedById = userId;
            ChangedAt = at;
            ChangedSinceApproval = false;
            return Version;
        }

        private void Changed(DateTime at)
        {
            ChangedAt = at;
            ChangedSinceApproval = true;
        }

        // R5: overdue when the review date has passed
        public bool IsReviewOverdue(DateOnly today) => NextReviewOn is { } due && due < today;

        private static string? Clean(string? value, int max, string what)
        {
            var clean = (value ?? "").Trim();
            if (clean.Length > max)
            {
                throw new DomainException($"{what} must be {max} characters or fewer");
            }

            return clean.Length == 0 ? null : clean;
        }
    }

    // US-208 R2: one project's place in the scope, with the reason when it is outside
    public class ScopedProject
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ScopeId { get; private set; }
        public Guid ProjectId { get; private set; }
        public bool IsInside { get; private set; }
        public string? Reason { get; private set; }

        // For EF Core only
        private ScopedProject() { }

        internal static ScopedProject Inside(Guid scopeId, Guid projectId) => new() { ScopeId = scopeId, ProjectId = projectId, IsInside = true };

        internal static ScopedProject Outside(Guid scopeId, Guid projectId, string reason)
            => new() { ScopeId = scopeId, ProjectId = projectId, IsInside = false, Reason = reason };

        internal void MoveInside()
        {
            IsInside = true;
            Reason = null;
        }

        internal void MoveOutside(string reason)
        {
            IsInside = false;
            Reason = reason;
        }
    }
}
