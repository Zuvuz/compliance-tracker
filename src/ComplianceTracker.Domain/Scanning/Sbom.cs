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

        // Null when the system imported it: a pipeline with a token or GitHub (US-148)
        public Guid? UploadedById { get; private set; }

        // US-148 R5: where it came from; for a pipeline the token (and its name, kept for the list) and the commit
        public SbomSource Source { get; private set; } = SbomSource.Upload;
        public Guid? UploadTokenId { get; private set; }
        public string? UploadTokenName { get; private set; }
        public string? CommitSha { get; private set; }

        // US-148 R3: the version an SBOM from GitHub's dependency graph is stored for
        public const string GitHubVersion = "GitHub (default branch)";

        // R5: e.g. "Upload", "Pipeline (GitHub Actions)" or "GitHub"
        public string SourceText => Source switch
        {
            SbomSource.CiUpload => $"Pipeline ({UploadTokenName})",
            SbomSource.GitHub => "GitHub",
            _ => "Upload"
        };

        public IReadOnlyList<SbomComponent> Components => _components;

        // R7: e.g. "CycloneDX 1.6 · 214 components · version 2.1.0"
        public string Summary => $"{Format} · {_components.Count} {(_components.Count == 1 ? "component" : "components")} · version {ProductVersion}";

        // For EF Core only
        private Sbom() { }

        // R1, R2: the importer has read the file; the caller checks the format
        public static Sbom Create(Guid projectId, string? productVersion, string format, string? fileName, byte[] content,
            IEnumerable<SbomComponentRead> components, Guid? userId, DateTime at, SbomSource source = SbomSource.Upload)
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
                UploadedById = userId,
                Source = source
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

        // US-148 R1, R2: what a pipeline sent: its token and, optionally, the commit (a Git SHA of 7 to 40 hex characters)
        public void FromPipeline(Guid tokenId, string tokenName, string? commit)
        {
            var sha = string.IsNullOrWhiteSpace(commit) ? null : commit.Trim().ToLowerInvariant();
            if (sha is not null && (sha.Length is < 7 or > 40 || !sha.All(Uri.IsHexDigit)))
            {
                throw new DomainException("The commit must be a Git SHA of 7 to 40 hex characters");
            }

            Source = SbomSource.CiUpload;
            UploadTokenId = tokenId;
            UploadTokenName = Cut(tokenName, 100);
            CommitSha = sha;
        }
    }

    // US-148 R5
    public enum SbomSource
    {
        Upload,
        CiUpload,
        GitHub
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
