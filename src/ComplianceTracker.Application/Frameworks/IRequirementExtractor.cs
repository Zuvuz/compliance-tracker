namespace ComplianceTracker.Application.Frameworks
{
    // Finds requirements in the text of a framework PDF (US-007 R1). Implemented in Infrastructure.
    // No AI is used (domain model rule 9); the member checks and corrects the result before activating.
    public interface IRequirementExtractor
    {
        // The requirements in the document's order. Empty when none are found or the PDF has no text
        // (e.g. a scanned document); the member then adds them by hand (R5). Never throws for a bad PDF.
        IReadOnlyList<ExtractedRequirement> Extract(byte[] pdf);
    }

    // One requirement found in a PDF, e.g. ("A.5.15", "Access control", "Rules to control ...")
    public record ExtractedRequirement(string Code, string Title, string Description);
}
