using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Projects
{
    // A product the organisation builds and that must meet rules (US-008).
    // The intended purpose decides which requirements apply, e.g. under the CRA (Annex VII, point 1(a)).
    // Belongs to exactly one organisation and is invisible to all others.
    public class Project
    {
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
            // R2: name and intended purpose are required
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Project name is required");
            }

            if (string.IsNullOrWhiteSpace(intendedPurpose))
            {
                throw new DomainException("Intended purpose is required");
            }

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
    }
}
