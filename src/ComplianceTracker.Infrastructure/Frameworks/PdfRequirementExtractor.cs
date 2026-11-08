using ComplianceTracker.Application.Frameworks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ComplianceTracker.Infrastructure.Frameworks
{
    // Reads the text of a PDF's pages with PdfPig (US-007 R1); RequirementParser finds the requirements and what was left
    // (US-070). No AI (domain model rule 9).
    public class PdfRequirementExtractor : IRequirementExtractor
    {
        // A limit, so a huge document cannot fill the database
        private const int MaxPages = 500;

        public ExtractionResult Extract(byte[] pdf)
        {
            try
            {
                return RequirementParser.Parse(ReadPages(pdf));
            }
            catch (Exception)
            {
                // A PDF PdfPig cannot read is treated like one without text: the member adds requirements by hand
                return ExtractionResult.Empty;
            }
        }

        private static List<IReadOnlyList<string>> ReadPages(byte[] pdf)
        {
            using var document = PdfDocument.Open(pdf);
            return document.GetPages().Take(MaxPages)
                .Select(page => (IReadOnlyList<string>)ContentOrderTextExtractor.GetText(page).Split('\n'))
                .ToList();
        }
    }
}
