using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Frameworks
{
    // En uploadet frameworkfil, der er tjekket: PDF, højst 20 MB, og et sikkert filnavn (US-005 R5, R6)

    public sealed class CatalogFile
    {
        public const long MaxSizeInBytes = 20 * 1024 * 1024;
        private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");
        public string FileName { get; }
        public byte[] Content { get; }
        public string Sha256 { get; }

        private CatalogFile(string fileName, byte[] content)
        {
            FileName = fileName;
            Content = content;
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        public static CatalogFile Create(string fileName, byte[] content)
        {
            if(content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The file must be 20 MB or smaller");
            }

            var hasPdfExtension = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var startsLikePdf = content.AsSpan().StartsWith(PdfSignature);
            if(!hasPdfExtension || !startsLikePdf)
            {
                throw new DomainException("Only PDF files are allowed");
            }

            return new CatalogFile(SafeFileName(fileName), content);
        }

        private static string SafeFileName(string fileName)
        {
            var normalised = fileName.Replace('\\', '/');
            var name = normalised[(normalised.LastIndexOf('/') + 1)..].Trim();
            return string.IsNullOrEmpty(name) ? "document.pdf" : name;
        }
    }
}
