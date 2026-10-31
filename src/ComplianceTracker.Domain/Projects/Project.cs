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
        public ProjectChange? ChangePhase(LifecyclePhase phase)
        {
            EnsureOpen();
            EnsureKnown(phase);

            if (phase == Phase)
            {
                return null;
            }

            var change = new ProjectChange("Phase", Phase.ToString(), phase.ToString());
            Phase = phase;
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
