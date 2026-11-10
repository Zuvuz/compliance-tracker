using ComplianceTracker.Domain.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Frameworks
{
    // US-143 R2: the forms a standard can be uploaded in
    public enum CatalogFormat
    {
        Pdf,
        Word,
        Excel,
        Csv,
        Html,

        // US-137: an EU act's Formex package as fetched from EUR-Lex; never uploaded by a member
        Formex
    }

    // An uploaded framework file that has passed every check: a PDF (US-005), or since US-143 a Word, Excel, CSV or HTML
    // file, of at most 20 MB with a safe file name (US-005 R5, R6). It is a value object: if one exists, it is valid,
    // so the rest of the code never has to check the file again.
    public sealed class CatalogFile
    {
        public const string WrongType = "Only PDF, Word (.docx), Excel (.xlsx), CSV or HTML files are allowed";

        // ASVS V5.2.1: only accept files the app can handle
        public const long MaxSizeInBytes = 20 * 1024 * 1024;

        // Every PDF starts with these bytes ("magic bytes")
        private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

        // .docx and .xlsx are zip packages
        private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

        // The cleaned file name, only used for display and downloads
        public string FileName { get; }

        public byte[] Content { get; }

        // Fingerprint of the content. An auditor can recalculate it to prove the file has not changed.
        public string Sha256 { get; }

        public CatalogFormat Format { get; }

        // For downloads
        public string ContentType => ContentTypeOf(Format);

        private CatalogFile(string fileName, byte[] content, CatalogFormat format)
        {
            FileName = fileName;
            Format = format;
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

            // ASVS V5.2.2: both the extension and the content must say the same type.
            // A text file renamed to .pdf is rejected, whatever content type the browser claims.
            var format = FormatOf(fileName);
            if (format is null || !ContentMatches(format.Value, content))
            {
                throw new DomainException(WrongType);
            }

            return new CatalogFile(SafeFileName(fileName), content, format.Value);
        }

        // US-137: the package an act was imported in from EUR-Lex, kept as the version's file
        public static CatalogFile FromEurLex(string celex, byte[] package)
        {
            if (package.Length == 0 || package.Length > MaxSizeInBytes * 2)
            {
                throw new DomainException("The act from EUR-Lex is empty or too large");
            }

            return new CatalogFile($"{celex}.formex.zip", package, CatalogFormat.Formex);
        }

        // The format by the file's extension, or null for any other
        public static CatalogFormat? FormatOf(string fileName)
        {
            if ((fileName ?? "").EndsWith(".formex.zip", StringComparison.OrdinalIgnoreCase))
            {
                return CatalogFormat.Formex;
            }

            var extension = Path.GetExtension(fileName ?? "");
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => CatalogFormat.Pdf,
                ".docx" => CatalogFormat.Word,
                ".xlsx" => CatalogFormat.Excel,
                ".csv" => CatalogFormat.Csv,
                ".html" or ".htm" => CatalogFormat.Html,
                _ => null
            };
        }

        public static string ContentTypeOf(CatalogFormat format) => format switch
        {
            CatalogFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CatalogFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CatalogFormat.Csv => "text/csv",
            CatalogFormat.Html => "text/plain", // never served as a page, so a saved page cannot run in the tracker
            CatalogFormat.Formex => "application/zip",
            _ => "application/pdf"
        };

        private static bool ContentMatches(CatalogFormat format, byte[] content) => format switch
        {
            CatalogFormat.Pdf => content.AsSpan().StartsWith(PdfSignature),
            CatalogFormat.Word => IsPackage(content, "word/document.xml"),
            CatalogFormat.Excel => IsPackage(content, "xl/workbook.xml"),
            CatalogFormat.Csv => IsText(content) && content.Length > 0,
            CatalogFormat.Html => IsText(content) && LooksLikeHtml(content),
            _ => false // a Formex package only comes from EUR-Lex (FromEurLex)
        };

        // A zip package with the part a Word or Excel file always has. Only the list of entries is read, nothing is unpacked.
        private static bool IsPackage(byte[] content, string part)
        {
            if (!content.AsSpan().StartsWith(ZipSignature))
            {
                return false;
            }

            try
            {
                using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
                return zip.Entries.Any(e => string.Equals(e.FullName, part, StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        // Text: no NUL bytes and not a PDF or a zip package with another name
        private static bool IsText(byte[] content)
            => !content.AsSpan().StartsWith(PdfSignature) && !content.AsSpan().StartsWith(ZipSignature) && Array.IndexOf(content, (byte)0) < 0;

        private static bool LooksLikeHtml(byte[] content)
        {
            var start = Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 64 * 1024));
            return start.Contains("<html", StringComparison.OrdinalIgnoreCase) || start.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase);
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
