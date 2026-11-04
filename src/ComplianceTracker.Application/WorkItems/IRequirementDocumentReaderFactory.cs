using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // US-083 (B36): gives the reader for a file type and a kind of document, e.g. (".docx", UseCases) → the use case reader.
    // Implemented in Infrastructure, where the readers are registered; Application only asks for one.
    // A new format is one new reader class and one registration, with no change here or in the use case.
    public interface IRequirementDocumentReaderFactory
    {
        // Every file type and kind that has a reader
        IReadOnlyCollection<(string Extension, DocumentKind Kind)> Supported { get; }

        // The reader, or null when there is none (the use case tells the user which files it takes)
        IRequirementDocumentReader? For(string extension, DocumentKind kind);
    }
}
