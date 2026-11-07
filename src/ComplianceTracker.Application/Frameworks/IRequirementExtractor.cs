namespace ComplianceTracker.Application.Frameworks
{
    // Finds requirements in an uploaded PDF (US-007 R1), and what it did not use (US-070). Implemented in Infrastructure.
    public interface IRequirementExtractor
    {
        // The requirements in the document's order, and the check of what was left. Empty when none are found or the PDF
        // has no text (e.g. a scanned document); the member then adds them by hand (R5). Never throws for a bad PDF.
        ExtractionResult Extract(byte[] pdf);
    }

    // One requirement found in a PDF, e.g. ("A.5.15", "Access control", "Rules to control ...")
    public record ExtractedRequirement(string Code, string Title, string Description);
}
