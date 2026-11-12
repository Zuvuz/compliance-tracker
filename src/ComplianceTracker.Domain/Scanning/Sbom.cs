using System.Security.Cryptography;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Scanning
{
    // US-079: a software bill of materials (CycloneDX or SPDX) for one version of the product (CRA Annex I, Part II (1)).
    // Its own aggregate with its components; the file is stored next to it (SbomFile) so the list does not load it.
    // A new SBOM for the same version replaces the shown one, and the old one is kept (R2).
    public class Sbom
    {
        public const long MaxSizeInBytes = 50 * 1024 * 1024;
        public const int MaxVersionLength = 100;

        private readonly List<SbomComponent> _components = [];

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string ProductVersion { get; private set; } = "";
        // e.g. "CycloneDX 1.6" or "SPDX 2.3"
        public string Format { get; private set; } = "";
        public string FileName { get; private set; } = "";
        public string Sha256 { get; private set; } = "";
        public DateTime ImportedAt { get; private set; }
        public Guid UploadedById { get; private set; }

        public IReadOnlyList<SbomComponent> Components => _components;

        // R7: e.g. "CycloneDX 1.6 · 214 components · version 2.1.0"
        public string Summary => $"{Format} · {_components.Count} {(_components.Count == 1 ? "component" : "components")} · version {ProductVersion}";

        // For EF Core only
        private Sbom() { }

        // R1, R2: the importer has read the file; the caller checks the format
        public static Sbom Create(Guid projectId, string? productVersion, string format, string? fileName, byte[] content,
            IEnumerable<SbomComponentRead> components, Guid userId, DateTime at)
        {
            if (content.Length == 0)
            {
                throw new DomainException("The file is empty");
            }

            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The SBOM is larger than 50 MB");
            }

            var version = productVersion?.Trim() ?? "";
            if (version.Length == 0)
            {
                throw new DomainException("Enter the product version the SBOM is for");
            }

            if (version.Length > MaxVersionLength)
            {
                throw new DomainException($"The version must be {MaxVersionLength} characters or fewer");
            }

            var sbom = new Sbom
            {
                ProjectId = projectId,
                ProductVersion = version,
                Format = Cut(format.Trim(), 50),
                FileName = Cut(Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "sbom.json" : fileName), 255),
                Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                ImportedAt = at,
                UploadedById = userId
            };

            // R3: the same component (package URL, or name and version) is listed once
            foreach (var read in components
                         .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                         .DistinctBy(c => (c.PackageUrl ?? $"{c.Name}@{c.Version}").ToLowerInvariant()))
            {
                sbom._components.Add(new SbomComponent(sbom.Id, read));
            }

            return sbom;
        }

        internal static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
    }

    // US-079 R3: one component as listed in the SBOM
    public class SbomComponent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid SbomId { get; private set; }
        public string Name { get; private set; } = "";
        public string? Version { get; private set; }
        public string? PackageUrl { get; private set; }
        public string? License { get; private set; }
        public string? Supplier { get; private set; }

        // For EF Core only
        private SbomComponent() { }

        internal SbomComponent(Guid sbomId, SbomComponentRead read)
        {
            SbomId = sbomId;
            Name = Sbom.Cut(read.Name.Trim(), 300);
            Version = Clean(read.Version, 200);
            PackageUrl = Clean(read.PackageUrl, 1000);
            License = Clean(read.License, 300);
            Supplier = Clean(read.Supplier, 300);
        }

        // R6: an open finding names the component: its name as a whole word and, when known, its version
        public bool IsNamedIn(string? text)
        {
            if (string.IsNullOrEmpty(text) || !ContainsWord(text, Name))
            {
                return false;
            }

            return Version is null || ContainsWord(text, Version);
        }

        private static bool ContainsWord(string text, string word)
        {
            for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                var before = at == 0 ? ' ' : text[at - 1];
                var after = at + word.Length >= text.Length ? ' ' : text[at + word.Length];
                if (!IsNamePart(before) && !IsNamePart(after))
                {
                    return true;
                }
            }

            return false;
        }

        // Letters, digits and the characters package names and versions are made of
        private static bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '+';

        private static string? Clean(string? value, int max)
            => string.IsNullOrWhiteSpace(value) || value.Trim() == "NOASSERTION" ? null : Sbom.Cut(value.Trim(), max);
    }

    // What an importer read for one component
    public record SbomComponentRead(string Name, string? Version, string? PackageUrl, string? License, string? Supplier);

    public class SbomFile
    {
        public Guid SbomId { get; private set; }
        public byte[] Content { get; private set; } = [];

        // For EF Core only
        private SbomFile() { }

        public SbomFile(Guid sbomId, byte[] content)
        {
            SbomId = sbomId;
            Content = content;
        }
    }
}
