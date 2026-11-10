using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // Finds requirements in an uploaded file (US-007 R1), and what it did not use (US-070). Implemented in Infrastructure.
    // US-143: a PDF, Word, Excel, CSV or HTML file.
    public interface IRequirementExtractor
    {
        public const string NoText = "This PDF has no text. Upload a PDF with text, or the standard as Word or Excel";

        // The requirements in the document's order, and the check of what was left. Empty when none are found or the file
        // cannot be read; the member then adds them by hand (US-007 R5). US-143 R2: throws a DomainException (NoText) for
        // a PDF whose pages have no text, such as a scan. An Excel or CSV file needs its columns; without them it is empty.
        ExtractionResult Extract(CatalogFile file, TableColumns? columns = null);

        // US-143 R7: the header row of an Excel or CSV file, so the admin can choose the columns; empty for other files
        IReadOnlyList<string> Headers(CatalogFile file);
    }

    // One requirement found in a file, e.g. ("A.5.15", "Access control", "Rules to control ..."). US-143: its kind and the
    // code of the requirement it sits under (null at the top).
    public record ExtractedRequirement(
        string Code, string Title, string Description, RequirementKind Kind = RequirementKind.Requirement, string? ParentCode = null);
}
