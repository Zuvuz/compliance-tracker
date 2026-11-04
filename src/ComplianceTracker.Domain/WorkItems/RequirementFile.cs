using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.WorkItems
{
    // US-032 R1: an uploaded requirement specification that has passed every check: at most 10 MB, a known type whose
    // content matches its extension, and a safe file name. A value object: if one exists, it is valid.
    public sealed class RequirementFile
    {
        // ASVS V5.2.1: only accept files the app can handle
        public const long MaxSizeInBytes = 10 * 1024 * 1024;

        // Extension → content type, in the order the error message names them
        private static readonly (string Extension, string ContentType)[] Types =
        [
            (".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            (".csv", "text/csv")
        ];

        // .xlsx is a ZIP file
        private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

        // The cleaned file name, only used for display and downloads
        public string FileName { get; }

        // Lower case, e.g. ".xlsx"
        public string Extension { get; }

        public string ContentType { get; }

        public byte[] Content { get; }

        public string Sha256 { get; }

        // R1, e.g. "Only .xlsx and .csv files can be uploaded"
        public static string AllowedMessage
        {
            get
            {
                var names = Types.Select(t => t.Extension).ToList();
                var list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
                return $"Only {list} files can be uploaded";
            }
        }

        private RequirementFile(string fileName, string extension, string contentType, byte[] content)
        {
            FileName = fileName;
            Extension = extension;
            ContentType = contentType;
            Content = content;
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        public static RequirementFile Create(string fileName, byte[] content)
        {
            var name = SafeFileName(fileName);
            var extension = Path.GetExtension(name).ToLowerInvariant();
            var type = Types.FirstOrDefault(t => t.Extension == extension);
            if (type.Extension is null)
            {
                throw new DomainException(AllowedMessage);
            }

            if (content.Length == 0)
            {
                throw new DomainException("The file is empty");
            }

            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The file is larger than 10 MB");
            }

            // ASVS V5.2.2: the content must agree with the extension, whatever the browser claims
            if (!ContentMatches(extension, content))
            {
                throw new DomainException($"The file is not a valid {extension} file");
            }

            return new RequirementFile(name, extension, type.ContentType, content);
        }

        private static bool ContentMatches(string extension, byte[] content)
        {
            if (extension != ".csv")
            {
                return content.AsSpan().StartsWith(ZipSignature);
            }

            // Text: valid UTF-8 and no NUL bytes, so a binary file cannot pass as .csv
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

        // ASVS V5.3.2: never trust the user's file name. Folders and control characters are removed.
        private static string SafeFileName(string fileName)
        {
            var normalised = (fileName ?? "").Replace('\\', '/');
            var name = normalised[(normalised.LastIndexOf('/') + 1)..].Trim();
            name = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray());
            if (name.Length > 200)
            {
                name = name[^200..];
            }

            return string.IsNullOrEmpty(name) ? "requirements" : name;
        }
    }
}
