using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // Records that an organisation has switched a built-in framework on (US-006).
    // Switching it off sets DisabledAt; the row is never deleted, so the history is kept (R4).
    // Switching it on again creates a new row.
    public class OrganizationFramework
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // The built-in framework's version that was switched on
        public Guid FrameworkVersionId { get; private set; }

        public DateTime EnabledAt { get; private set; }
        public Guid EnabledById { get; private set; }

        // Set when the framework is switched off again
        public DateTime? DisabledAt { get; private set; }
        public Guid? DisabledById { get; private set; }

        // Switched on and not yet switched off
        public bool IsActive => DisabledAt is null;

        // For EF Core only
        private OrganizationFramework() { }

        public static OrganizationFramework Enable(Guid organizationId, Guid frameworkVersionId, Guid enabledById, DateTime enabledAt)
            => new()
            {
                OrganizationId = organizationId,
                FrameworkVersionId = frameworkVersionId,
                EnabledById = enabledById,
                EnabledAt = enabledAt
            };

        public void Disable(Guid disabledById, DateTime disabledAt)
        {
            if (!IsActive)
            {
                throw new DomainException("The framework is already switched off");
            }

            DisabledById = disabledById;
            DisabledAt = disabledAt;
        }
    }
}
