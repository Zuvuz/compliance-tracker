using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Frameworks
{
    // An uploaded framework file that has passed every check: a PDF of at most 20 MB
    // with a safe file name (US-005 R5, R6). It is a value object: if one exists, it is valid,
    // so the rest of the code never has to check the file again.
    public sealed class CatalogFile
    {
        // ASVS V5.2.1: only accept files the app can handle
        public const long MaxSizeInBytes = 20 * 1024 * 1024;

        // Every PDF starts with these bytes ("magic bytes")
        private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

        // The cleaned file name, only used for display and downloads
        public string FileName { get; }

        public byte[] Content { get; }

        // Fingerprint of the content. An auditor can recalculate it to prove the file has not changed.
        public string Sha256 { get; }

        private CatalogFile(string fileName, byte[] content)
        {
            FileName = fileName;
            Content = content;
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        // The only way to create a CatalogFile, so the rules below always run
        public static CatalogFile Create(string fileName, byte[] content)
        {
            if(content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The file must be 20 MB or smaller");
            }

            // ASVS V5.2.2: both the extension and the content must say "PDF".
            // A text file renamed to .pdf is rejected, whatever content type the browser claims.
            var hasPdfExtension = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var startsLikePdf = content.AsSpan().StartsWith(PdfSignature);
            if(!hasPdfExtension || !startsLikePdf)
            {
                throw new DomainException("Only PDF files are allowed");
            }

            return new CatalogFile(SafeFileName(fileName), content);
        }

        // ASVS V5.3.2: never trust the user's file name. Folders are removed,
        // e.g. "../../policy.pdf" becomes "policy.pdf". Files are stored by id, not by this name.
        private static string SafeFileName(string fileName)
        {
            var normalised = fileName.Replace('\\', '/');
            var name = normalised[(normalised.LastIndexOf('/') + 1)..].Trim();
            return string.IsNullOrEmpty(name) ? "document.pdf" : name;
        }
    }
}
