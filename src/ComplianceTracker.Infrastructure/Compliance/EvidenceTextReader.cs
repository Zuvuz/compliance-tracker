using ComplianceTracker.Application.Compliance;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ComplianceTracker.Infrastructure.Compliance
{
    // US-059 R3: the text the AI reads from an evidence file. PDF with PdfPig (as US-007), text files as UTF-8.
    // Images give no text; scanned PDFs (OCR) are out of scope. Only the first pages and characters are read.
    public class EvidenceTextReader : IEvidenceTextReader
    {
        private const int MaxPages = 20;
        private const int MaxCharacters = 8000;

        public string? Read(string contentType, byte[] content)
        {
            try
            {
                var text = contentType switch
                {
                    "application/pdf" => Pdf(content),
                    "text/plain" or "text/csv" or "text/markdown" => new UTF8Encoding(false, false).GetString(content),
                    _ => null
                };
                return string.IsNullOrWhiteSpace(text) ? null : text.Length <= MaxCharacters ? text : text[..MaxCharacters];
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // A PDF PdfPig cannot read is matched by its title only
                return null;
            }
        }

        private static string Pdf(byte[] content)
        {
            using var document = PdfDocument.Open(content);
            var text = new StringBuilder();
            foreach (var page in document.GetPages().Take(MaxPages))
            {
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
                if (text.Length > MaxCharacters)
                {
                    break;
                }
            }

            return text.ToString();
        }
    }
}
