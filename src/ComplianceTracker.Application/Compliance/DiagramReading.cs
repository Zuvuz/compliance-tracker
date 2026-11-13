using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-149: reads one diagram format (implemented in Infrastructure). Throws DiagramUnreadableException with a reason
    // the user can act on when the file cannot be read. Never runs anything in the file (R2).
    public interface IDiagramReader
    {
        DiagramFormat Format { get; }

        DiagramContent Read(byte[] content);
    }

    // R4: why a diagram could not be read, e.g. "the XML is not well-formed"
    public class DiagramUnreadableException : Exception
    {
        public DiagramUnreadableException(string reason, Exception? inner = null) : base(reason, inner)
        {
        }
    }

    // The readers, picked by the file's content type
    public class DiagramReaders
    {
        private readonly IReadOnlyDictionary<DiagramFormat, IDiagramReader> _readers;

        public DiagramReaders(IEnumerable<IDiagramReader> readers)
        {
            _readers = readers.ToDictionary(r => r.Format);
        }

        public static bool IsDiagram(string? contentType) => DiagramContent.FormatOf(contentType) is not null;

        // The content, or why it could not be read; null for a file that is not a diagram
        public (DiagramContent? Content, string? Error)? Read(string? contentType, byte[] content)
        {
            if (DiagramContent.FormatOf(contentType) is not { } format || !_readers.TryGetValue(format, out var reader))
            {
                return null;
            }

            try
            {
                return (reader.Read(content), null);
            }
            catch (DiagramUnreadableException error)
            {
                return (null, error.Message);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return (null, $"the file is not a {format} diagram the tracker can read");
            }
        }
    }
}
