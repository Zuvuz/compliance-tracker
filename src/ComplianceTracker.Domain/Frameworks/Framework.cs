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

        // The newest upload, whether it is a draft or active. Never null, because a framework always has a version.
        public FrameworkVersion LatestVersion => _versions.MaxBy(v => v.Number)!;

        // The version that applies on a day (US-007 R4, US-035 R3, R4): of the active versions whose start date
        // has come, the one that started applying last. If none has started yet, the first upcoming one (US-035 R6).
        // Null while the framework only has drafts. Calculated, not stored.
        public FrameworkVersion? CurrentVersion(DateOnly today)
        {
            var applying = _versions.Where(v => v.AppliesOn(today)).ToList();
            if (applying.Count > 0)
            {
                return applying.OrderBy(v => v.StartsApplyingAt).ThenBy(v => v.Number).Last();
            }

            return _versions.Where(v => v.IsActive).OrderBy(v => v.AppliesFrom).ThenBy(v => v.Number).FirstOrDefault();
        }

        // US-035 R3: an active version that has not started yet and is not the current one, or null
        public FrameworkVersion? UpcomingVersion(DateOnly today)
        {
            var current = CurrentVersion(today);
            return _versions
                .Where(v => v.IsUpcomingOn(today) && v != current)
                .OrderBy(v => v.AppliesFrom)
                .FirstOrDefault();
        }

        // The current version, or a clear error when there is none yet (e.g. a project tries to use the framework)
        public FrameworkVersion RequireCurrentVersion(DateOnly today)
            => CurrentVersion(today) ?? throw new DomainException($"{Name} has no active version yet");

        // A version of this framework, or an error
        public FrameworkVersion Version(Guid versionId)
            => _versions.FirstOrDefault(v => v.Id == versionId)
               ?? throw new DomainException("Framework version not found");

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
