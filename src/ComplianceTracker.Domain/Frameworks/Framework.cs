using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // A set of rules the organisation follows, e.g. "ISO/IEC 27001" or "Security policy" (US-005).
    // Two kinds:
    // - uploaded: owned by one organisation and invisible to all others;
    // - built-in: freely available regulations such as CRA and the AI Act, shared by everyone,
    //   with no owner (US-006). They are added by a database migration, never by users.
    public class Framework
    {
        // Private list, so versions can only be added through AddVersion
        private readonly List<FrameworkVersion> _versions = new();

        public Guid Id { get; } = Guid.NewGuid();

        // Null for built-in frameworks
        public Guid? OwnerOrganizationId { get; private set; }

        public string Name { get; private set; } = "";
        public IReadOnlyList<FrameworkVersion> Versions => _versions;

        // Built-in frameworks have no owner (US-006)
        public bool IsBuiltIn => OwnerOrganizationId is null;

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
            // US-006 R5: built-in frameworks follow the published regulation and cannot be changed
            if (IsBuiltIn)
            {
                throw new DomainException("Built-in frameworks cannot be changed");
            }

            var version = new FrameworkVersion(Id, _versions.Count + 1, file, uploadedById, uploadedAt);
            _versions.Add(version);
            return version;
        }
    }
}
