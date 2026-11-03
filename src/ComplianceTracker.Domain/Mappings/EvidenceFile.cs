using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Mappings
{
    // An uploaded evidence file that has passed every check (US-057 R2): at most 10 MB, a known type whose
    // content matches its extension, and a safe file name. A value object: if one exists, it is valid.
    public sealed class EvidenceFile
    {
        // ASVS V5.2.1: only accept files the app can handle
        public const long MaxSizeInBytes = 10 * 1024 * 1024;

        // Extension → content type. Text types are checked to be UTF-8 text; the others by their first bytes.
        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".md"] = "text/markdown"
        };

        private static readonly Dictionary<string, byte[]> Signatures = new()
        {
            ["application/pdf"] = Encoding.ASCII.GetBytes("%PDF-"),
            ["image/png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            ["image/jpeg"] = [0xFF, 0xD8, 0xFF]
        };

        // The cleaned file name, only used for display and downloads
        public string FileName { get; }

        public string ContentType { get; }

        public byte[] Content { get; }

        // Fingerprint of the content, so an auditor can prove the file has not changed
        public string Sha256 { get; }

        private EvidenceFile(string fileName, string contentType, byte[] content)
        {
            FileName = fileName;
            ContentType = contentType;
            Content = content;
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        public static EvidenceFile Create(string fileName, byte[] content)
        {
            if (content.Length == 0)
            {
                throw new DomainException("The file is empty");
            }

            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The file is larger than 10 MB");
            }

            var name = SafeFileName(fileName);
            // ASVS V5.2.2: both the extension and the content must agree, whatever the browser claims
            if (!ContentTypes.TryGetValue(Path.GetExtension(name), out var contentType) || !ContentMatches(contentType, content))
            {
                throw new DomainException("Only PDF, PNG, JPG, TXT, CSV and Markdown files are allowed");
            }

            return new EvidenceFile(name, contentType, content);
        }

        private static bool ContentMatches(string contentType, byte[] content)
        {
            if (Signatures.TryGetValue(contentType, out var signature))
            {
                return content.AsSpan().StartsWith(signature);
            }

            // Text: valid UTF-8 and no NUL bytes, so a binary file cannot pass as .txt
            if (content.AsSpan().IndexOf((byte)0) >= 0)
            {
                return false;
            }

            try
            {
                _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        // ASVS V5.3.2: never trust the user's file name. Folders are removed; files are stored by id.
        private static string SafeFileName(string fileName)
        {
            var normalised = fileName.Replace('\\', '/');
            var name = normalised[(normalised.LastIndexOf('/') + 1)..].Trim();
            name = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray());
            if (name.Length > 200)
            {
                name = name[^200..];
            }

            return string.IsNullOrEmpty(name) ? "evidence" : name;
        }
    }
}
