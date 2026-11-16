using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Application.Reports
{
    // US-180: signs and checks a detached signature with the organisation's seal (implemented in Infrastructure with CMS)
    public interface IPackageSigner
    {
        byte[] Sign(byte[] data, byte[] pfx, string password, DateTime atUtc);

        // Null when the signature is not a valid signature over the data
        PackageSignature? Verify(byte[] data, byte[] signature);
    }

    public sealed record PackageSignature(string Subject, DateTime? SignedAtUtc);

    public sealed record ManifestFile(string Path, string Sha256);

    // R1: what the package holds and who made it
    public sealed record Manifest(
        string Package, string Organisation, string Project, string MadeBy, DateTime MadeAtUtc, bool Signed, IReadOnlyList<ManifestFile> Files);

    public enum FileDifference
    {
        Changed,
        Missing,
        Added
    }

    // R4: what a check found
    public sealed record PackageCheck(Manifest? Manifest, bool Signed, PackageSignature? Signature, IReadOnlyList<(FileDifference Kind, string Path)> Differences,
        string? Problem)
    {
        public bool SignatureValid => Signed && Signature is not null;

        // R4: "Unchanged" only with a valid signature and every file matching
        public bool Unchanged => Problem is null && SignatureValid && Differences.Count == 0;

        public bool FilesMatch => Problem is null && Manifest is not null && Differences.Count == 0;
    }

    // US-180: the manifest of an audit package, and the check of a package a receiver uploads. Pure, apart from the signer.
    public static class PackageManifest
    {
        public const string ManifestPath = "manifest.json";
        public const string SignaturePath = "manifest.json.p7s";

        // R3: the largest package that can be checked, and the most its files may unpack to (a ZIP bomb is refused)
        public const long MaxSizeInBytes = 200L * 1024 * 1024;
        public const long MaxUnpackedBytes = 2L * 1024 * 1024 * 1024;
        public const int MaxFiles = 20_000;

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // R1: every file in the package, sorted by path, so the same files give the same manifest
        public static Manifest Create(string package, string organisation, string project, string madeBy, DateTime madeAtUtc, bool signed,
            IEnumerable<(string Path, byte[] Content)> files)
            => new(package, organisation, project, madeBy, madeAtUtc, signed,
                files.Where(f => !IsManifestPart(f.Path))
                    .Select(f => new ManifestFile(f.Path, Sha256(f.Content)))
                    .OrderBy(f => f.Path, StringComparer.Ordinal)
                    .ToList());

        public static byte[] ToJson(Manifest manifest) => JsonSerializer.SerializeToUtf8Bytes(manifest, Json);

        public static Manifest? FromJson(byte[] json)
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<Manifest>(json, Json);
                return manifest?.Files is null ? null : manifest;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static bool IsManifestPart(string path) => path is ManifestPath or SignaturePath;

        // R4: each file that changed, is missing or was added, sorted by path
        public static IReadOnlyList<(FileDifference Kind, string Path)> Compare(Manifest manifest, IReadOnlyDictionary<string, byte[]> files)
        {
            var differences = new List<(FileDifference, string)>();
            var expected = manifest.Files.GroupBy(f => f.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Sha256, StringComparer.Ordinal);
            foreach (var (path, sha) in expected)
            {
                if (!files.TryGetValue(path, out var content))
                {
                    differences.Add((FileDifference.Missing, path));
                }
                else if (!string.Equals(Sha256(content), sha, StringComparison.OrdinalIgnoreCase))
                {
                    differences.Add((FileDifference.Changed, path));
                }
            }

            differences.AddRange(files.Keys.Where(p => !IsManifestPart(p) && !expected.ContainsKey(p)).Select(p => (FileDifference.Added, p)));
            return differences.OrderBy(d => d.Item2, StringComparer.Ordinal).ThenBy(d => d.Item1).ToList();
        }

        // R3, R4: checks a package; nothing is stored
        public static PackageCheck Check(byte[] zip, IPackageSigner signer)
        {
            if (zip.Length == 0)
            {
                throw new DomainException("Choose the audit package (a .zip file) to check");
            }

            if (zip.Length > MaxSizeInBytes)
            {
                throw new DomainException($"The file is larger than {MaxSizeInBytes / 1024 / 1024} MB");
            }

            Dictionary<string, byte[]> files;
            try
            {
                files = Read(zip);
            }
            catch (InvalidDataException)
            {
                return new PackageCheck(null, false, null, [], "The file is not a ZIP file");
            }

            if (!files.TryGetValue(ManifestPath, out var json) || FromJson(json) is not { } manifest)
            {
                return new PackageCheck(null, false, null, [], "The package has no manifest.json, so it cannot be checked");
            }

            var signed = files.TryGetValue(SignaturePath, out var signature);
            var valid = signed ? signer.Verify(json, signature!) : null;
            return new PackageCheck(manifest, signed, valid, Compare(manifest, files), null);
        }

        // The entries of a ZIP, refusing one that unpacks to too much or has too many files
        public static Dictionary<string, byte[]> Read(byte[] zip)
        {
            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxFiles)
            {
                throw new DomainException("The package has too many files to check");
            }

            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long total = 0;
            foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith('/')))
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > MaxUnpackedBytes)
                    {
                        throw new DomainException("The package unpacks to more than can be checked");
                    }

                    memory.Write(buffer, 0, read);
                }

                files[entry.FullName] = memory.ToArray();
            }

            return files;
        }
    }
}
