using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;

namespace ComplianceTracker.Domain.Feedback
{
    // US-063 R3: a screenshot sent with feedback. PNG or JPG, at most 5 MB, checked by its content (as US-057 R2).
    // A value object: if one exists, it is valid.
    public sealed class Screenshot
    {
        public const long MaxSizeInBytes = 5 * 1024 * 1024;

        private static readonly Dictionary<string, (string ContentType, byte[] Signature)> Types = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF])
        };

        public string FileName { get; }
        public string ContentType { get; }
        public byte[] Content { get; }
        public string Sha256 { get; }

        private Screenshot(string fileName, string contentType, byte[] content)
        {
            FileName = fileName;
            ContentType = contentType;
            Content = content;
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        public static Screenshot Create(string fileName, byte[] content)
        {
            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The screenshot must be 5 MB or smaller");
            }

            // ASVS V5.3.2: never trust the user's file name; only its last part is kept, for display
            var name = Path.GetFileName((fileName ?? "").Replace('\\', '/').Split('/')[^1]).Trim();
            name = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray());
            if (name.Length > 200)
            {
                name = name[^200..];
            }

            // ASVS V5.2.2: both the extension and the content must agree
            if (content.Length == 0
                || !Types.TryGetValue(Path.GetExtension(name), out var type)
                || !content.AsSpan().StartsWith(type.Signature))
            {
                throw new DomainException("The screenshot must be a PNG or JPG image");
            }

            return new Screenshot(name, type.ContentType, content);
        }
    }
}
