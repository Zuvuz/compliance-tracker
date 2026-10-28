using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // A set of rules the organisation follows, e.g. "ISO/IEC 27001" or "Security policy" (US-005).
    // Owned by one organisation and invisible to all others.
    public class Framework
    {
        // Private list, so versions can only be added through AddVersion
        private readonly List<FrameworkVersion> _versions = new();

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OwnerOrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public IReadOnlyList<FrameworkVersion> Versions => _versions;

        // The newest version is the one that applies (R3). Calculated, not stored.
        public FrameworkVersion CurrentVersion => _versions.MaxBy(v => v.Number)!;

        // For EF Core only
        private Framework() { }

        // A framework is always created together with its first version, so it never has zero versions.
        // The rule that names are unique per organisation needs the database and lives in FrameworkService.
        public static Framework Create(Guid organizationId, string name, CatalogFile file, Guid uploadedById, DateTime uploadedAt)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Framework name is required");
            }

            var framework = new Framework { OwnerOrganizationId = organizationId, Name = name.Trim() };
            framework.AddVersion(file, uploadedById, uploadedAt);
            return framework;
        }

        // A new upload gets the next number; earlier versions are kept (R3)
        public FrameworkVersion AddVersion(CatalogFile file, Guid uploadedById, DateTime uploadedAt)
        {
            var version = new FrameworkVersion(Id, _versions.Count + 1, file, uploadedById, uploadedAt);
            _versions.Add(version);
            return version;
        }
    }
}
