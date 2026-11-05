using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Domain.Projects
{
    // A product the organisation builds and that must meet rules (US-008).
    // The intended purpose decides which requirements apply, e.g. under the CRA (Annex VII, point 1(a)).
    // Belongs to exactly one organisation and is invisible to all others.
    public class Project
    {
        // Every framework version the project has used, also removed ones (the history, US-011 R7)
        private readonly List<ProjectFramework> _frameworks = new();

        public Guid Id { get; } = Guid.NewGuid();

        // US-092: the row's version, set by the database (PostgreSQL's xmin). A form carries it, so a save
        // can tell that someone else changed the row after the form was opened.
        public uint Version { get; private set; }
        public Guid OrganizationId { get; private set; }

        // Unique within the organisation (checked in ProjectService, because it needs the database)
        public string Name { get; private set; } = "";

        // Free text about the product (optional)
        public string Description { get; private set; } = "";

        // What the product is meant to be used for. Required, because it decides which rules apply.
        public string IntendedPurpose { get; private set; } = "";

        // What is included in and excluded from the project (optional)
        public string Scope { get; private set; } = "";

        public LifecyclePhase Phase { get; private set; }

        // US-025: the end of the support period (CRA Art. 13(8), (19)), why it is shorter than five years if it is,
        // and the day the project was released, from which the five years count
        public DateOnly? SupportEndsOn { get; private set; }
        public string? SupportPeriodReason { get; private set; }
        public DateOnly? ReleasedOn { get; private set; }

        // US-074: when support ended, the last day the product was placed on the market, and until when the project and its
        // documentation must be kept (CRA Art. 13(13))
        public DateOnly? EndOfSupportOn { get; private set; }
        public DateOnly? LastPlacedOnMarketOn { get; private set; }
        public DateOnly? RetainUntil { get; private set; }

        // CRA Art. 13(13): at least 10 years after the product was placed on the market, or the support period if longer
        public const int RetentionYears = 10;

        // CRA Art. 13(8): at least five years, unless the product is expected to be in use for less
        public const int MinimumSupportYears = 5;
        public const int MaxSupportReasonLength = 1000;

        // US-018: the GitHub repository the project reads its user stories from, or null
        public GitHubLink? GitHub { get; private set; }

        // US-104 R2: how the project describes its requirements. A new project starts with all of them, so nothing is
        // hidden before someone chooses; there is always at least one.
        public RequirementMethods RequirementMethods { get; private set; } = RequirementMethods.All;

        // US-024 R4: a project at end of support is kept as it was and cannot be changed
        public bool IsClosed => Phase == LifecyclePhase.EndOfSupport;

        public IReadOnlyList<ProjectFramework> FrameworkHistory => _frameworks;

        // The framework versions the project follows now, one per framework (US-011 R3)
        public IEnumerable<ProjectFramework> Frameworks => _frameworks.Where(f => f.IsActive);

        // For EF Core only. Everyone else must use Create, so the rules always run.
        private Project() { }

        public static Project Create(
            Guid organizationId,
            string name,
            string? description,
            string intendedPurpose,
            string? scope,
            LifecyclePhase phase)
        {
            EnsureRequired(name, intendedPurpose);
            EnsureKnown(phase);

            return new Project
            {
                OrganizationId = organizationId,
                Name = name.Trim(),
                Description = description?.Trim() ?? "",
                IntendedPurpose = intendedPurpose.Trim(),
                Scope = scope?.Trim() ?? "",
                Phase = phase
            };
        }

        // ---------------------------------------------------------- Methods (US-104)

        // R2, R4: one or more methods. Turning one off hides it on the tab and deletes nothing (R5).
        // Returns the change, or null when nothing changed.
        public ProjectChange? ChangeRequirementMethods(RequirementMethods methods)
        {
            EnsureOpen();
            methods &= RequirementMethods.All;
            if (methods == RequirementMethods.None)
            {
                throw new DomainException("Choose at least one method");
            }

            if (methods == RequirementMethods)
            {
                return null;
            }

            var change = new ProjectChange("Methods", RequirementMethodNames.Describe(RequirementMethods), RequirementMethodNames.Describe(methods));
            RequirementMethods = methods;
            return change;
        }

        // ---------------------------------------------------------- Editing (US-024)

        // R1: the details, with the same rules as Create. Returns what changed (empty if nothing did),
        // so the caller can log the old and new values (R3).
        // The rule that names are unique needs the database and lives in ProjectService.
        public IReadOnlyList<ProjectChange> Update(string name, string? description, string intendedPurpose, string? scope)
        {
            EnsureOpen();
            EnsureRequired(name, intendedPurpose);

            var changes = new List<ProjectChange>();
            Name = Changed("Name", Name, name.Trim(), changes);
            Description = Changed("Description", Description, description?.Trim() ?? "", changes);
            IntendedPurpose = Changed("Intended purpose", IntendedPurpose, intendedPurpose.Trim(), changes);
            Scope = Changed("Scope", Scope, scope?.Trim() ?? "", changes);
            return changes;
        }

        // R2: any of the four phases, also backwards. EndOfSupport is final (R4).
        // Returns the change, or null if the project is already in that phase.
        // US-025 R1: moving to Released needs a support end date first (SetSupportPeriod). "today" is the release day.
        public ProjectChange? ChangePhase(LifecyclePhase phase, DateOnly? today = null)
        {
            EnsureOpen();
            EnsureKnown(phase);

            if (phase == Phase)
            {
                return null;
            }

            if (phase == LifecyclePhase.Released)
            {
                if (SupportEndsOn is null)
                {
                    throw new DomainException("Set the support end date before release");
                }

                ReleasedOn ??= today;
            }

            // US-074 R1: end of support sets the retention date, counted from today unless EndSupport was used
            if (phase == LifecyclePhase.EndOfSupport && today is { } day && RetainUntil is null)
            {
                Retain(LastPlacedOnMarketOn ?? day, null, day);
            }

            var change = new ProjectChange("Phase", Phase.ToString(), phase.ToString());
            Phase = phase;
            return change;
        }

        // ---------------------------------------------------------- End of support and retention (US-074)

        // R1: the product was last placed on the market on that day; the project is kept 10 years after it, or until the
        // end of the support period if that is later. A later date may be chosen, never an earlier one.
        public ProjectChange EndSupport(DateOnly lastPlacedOnMarket, DateOnly? keepUntil, DateOnly today)
        {
            EnsureOpen();
            Retain(lastPlacedOnMarket, keepUntil, today);
            var change = ChangePhase(LifecyclePhase.EndOfSupport, today)
                ?? throw new DomainException("The project is already at end of support");
            return new ProjectChange(change.Field, change.OldValue, $"{change.NewValue}, kept until {RetainUntil:dd-MM-yyyy}");
        }

        // R3: a project at end of support may not be archived or deleted before its retention date (used by US-028)
        public void EnsureMayBeRemoved(DateOnly today)
        {
            if (RetainUntil is { } until && today <= until)
            {
                throw new DomainException($"The project must be kept until {until:dd-MM-yyyy}");
            }
        }

        // The earliest date the rules allow
        public DateOnly MinimumRetention(DateOnly lastPlacedOnMarket)
        {
            var tenYears = lastPlacedOnMarket.AddYears(RetentionYears);
            return SupportEndsOn is { } supportEnds && supportEnds > tenYears ? supportEnds : tenYears;
        }

        private void Retain(DateOnly lastPlacedOnMarket, DateOnly? keepUntil, DateOnly today)
        {
            if (lastPlacedOnMarket > today)
            {
                throw new DomainException("The product cannot have been placed on the market after today");
            }

            if (ReleasedOn is { } released && lastPlacedOnMarket < released)
            {
                throw new DomainException($"The product was released on {released:dd-MM-yyyy}; it cannot have been placed on the market before that");
            }

            var minimum = MinimumRetention(lastPlacedOnMarket);
            if (keepUntil is { } chosen && chosen < minimum)
            {
                throw new DomainException($"The project must be kept until at least {minimum:dd-MM-yyyy}");
            }

            LastPlacedOnMarketOn = lastPlacedOnMarket;
            RetainUntil = keepUntil ?? minimum;
            EndOfSupportOn = today;
        }

        // ---------------------------------------------------------- Support period (US-025)

        // R2: under five years after release needs a reason. R3: the date can only move later. Returns the change, or
        // null when nothing changed. "today" is the release day while the project is not released yet.
        public ProjectChange? SetSupportPeriod(DateOnly endsOn, string? reason, DateOnly today)
        {
            EnsureOpen();
            if (endsOn <= today)
            {
                throw new DomainException("The support end date must be in the future");
            }

            if (SupportEndsOn is { } current && endsOn < current)
            {
                throw new DomainException("The support end date cannot be moved earlier");
            }

            var cleaned = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (cleaned?.Length > MaxSupportReasonLength)
            {
                throw new DomainException($"The reason must be {MaxSupportReasonLength} characters or fewer");
            }

            var from = ReleasedOn ?? today;
            if (endsOn < from.AddYears(MinimumSupportYears) && cleaned is null)
            {
                throw new DomainException("A support period under five years needs a reason");
            }

            if (endsOn == SupportEndsOn && cleaned == SupportPeriodReason)
            {
                return null;
            }

            var change = new ProjectChange("Support until",
                SupportEndsOn?.ToString("dd-MM-yyyy") ?? "", endsOn.ToString("dd-MM-yyyy"));
            SupportEndsOn = endsOn;
            SupportPeriodReason = cleaned;
            return change;
        }

        private static string Changed(string field, string oldValue, string newValue, List<ProjectChange> changes)
        {
            if (oldValue != newValue)
            {
                changes.Add(new ProjectChange(field, oldValue, newValue));
            }

            return newValue;
        }

        // US-008 R2: name and intended purpose are required
        private static void EnsureRequired(string name, string intendedPurpose)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Project name is required");
            }

            if (string.IsNullOrWhiteSpace(intendedPurpose))
            {
                throw new DomainException("Intended purpose is required");
            }
        }

        // Only the four phases the system knows, also if someone sends a number
        private static void EnsureKnown(LifecyclePhase phase)
        {
            if (!Enum.IsDefined(phase))
            {
                throw new DomainException("Unknown lifecycle phase");
            }
        }

        // US-024 R4: nothing about a project at end of support changes, also not its frameworks
        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }
        }

        // ---------------------------------------------------------- GitHub (US-018)

        // R1: links the project to a public repository, or moves it to another one.
        // Returns false when nothing changed. Work items from a previous repository are marked
        // "Removed" by the next sync, because their references name the old repository.
        public bool LinkGitHub(string repository, string? label, Guid userId, DateTime at)
        {
            EnsureOpen();
            var link = new GitHubLink(repository, label, userId, at, previous: GitHub);

            if (GitHub is not null
                && string.Equals(GitHub.Repository, link.Repository, StringComparison.OrdinalIgnoreCase)
                && GitHub.Label == link.Label)
            {
                return false;
            }

            GitHub = link;
            return true;
        }

        // ---------------------------------------------------------- Frameworks (US-011)

        // The version of this framework the project follows now, or null
        public ProjectFramework? FrameworkInUse(Guid frameworkId)
            => Frameworks.FirstOrDefault(f => f.FrameworkId == frameworkId);

        // R1, R2: the project starts following a version of a framework.
        // The caller makes sure the organisation may use the framework.
        public ProjectFramework UseFramework(Framework framework, FrameworkVersion version, Guid userId, DateTime at)
        {
            EnsureOpen();
            EnsureVersionOf(framework, version);

            // R3: never two versions of the same framework
            if (FrameworkInUse(framework.Id) is not null)
            {
                throw new DomainException($"The project already uses {framework.Name}");
            }

            var link = new ProjectFramework(Id, framework.Id, version.Id, userId, at);
            _frameworks.Add(link);
            return link;
        }

        // R4, R6: moves the project to another version of a framework it already uses.
        // The old link is kept as history. Returns false if the project already uses that version.
        public bool ChangeFrameworkVersion(Framework framework, FrameworkVersion version, Guid userId, DateTime at)
        {
            EnsureOpen();
            EnsureVersionOf(framework, version);

            var current = FrameworkInUse(framework.Id)
                ?? throw new DomainException($"The project does not use {framework.Name}");

            if (current.FrameworkVersionId == version.Id)
            {
                return false;
            }

            current.Remove(userId, at);
            _frameworks.Add(new ProjectFramework(Id, framework.Id, version.Id, userId, at));
            return true;
        }

        // R7: the project stops following a framework; the link is kept as history
        public void RemoveFramework(Framework framework, Guid userId, DateTime at)
        {
            EnsureOpen();
            var current = FrameworkInUse(framework.Id)
                ?? throw new DomainException($"The project does not use {framework.Name}");

            current.Remove(userId, at);
        }

        private static void EnsureVersionOf(Framework framework, FrameworkVersion version)
        {
            if (version.FrameworkId != framework.Id)
            {
                throw new DomainException("The version does not belong to the framework");
            }

            // US-007: a draft can still change, so projects only follow active versions
            if (!version.IsActive)
            {
                throw new DomainException("Only active versions can be used");
            }
        }
    }
}
