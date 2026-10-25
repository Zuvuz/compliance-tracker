using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // Et regelsæt, organisationen følger, fx "ISO/IEC 27001" eller "Security policy" (US-005)
    public class Framework
    {
        private readonly List<FrameworkVersion> _versions = new();

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OwnerOrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public IReadOnlyList<FrameworkVersion> Versions => _versions;

        // Den nyeste version er den, der gælder (R3)
        public FrameworkVersion CurrentVersion => _versions.MaxBy(v => v.Number)!;

        private Framework() { }

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

        // Ny version: næste nummer, de gamle bevares
        public FrameworkVersion AddVersion(CatalogFile file, Guid uploadedById, DateTime uploadedAt)
        {
            var version = new FrameworkVersion(Id, _versions.Count + 1, file, uploadedById, uploadedAt);
            _versions.Add(version);
            return version;
        }
    }
}