using ComplianceTracker.Domain.Common;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-033: the paragraphs of a .docx file in reading order, with whether each is a heading or a list item.
    // Only the body's own paragraphs are read: tables, images, headers and footers are left out (out of scope).
    // Deleted text from tracked changes is left out; inserted text is kept.
    public static class WordParagraphs
    {
        // One paragraph: its number in the document (1 = the first), its text, and how it is formatted
        public record Paragraph(int Position, string Text, bool IsHeading, bool IsListItem);

        public static IReadOnlyList<Paragraph> Read(byte[] content)
        {
            try
            {
                using var stream = new MemoryStream(content, writable: false);
                using var document = WordprocessingDocument.Open(stream, false, ExcelRequirementReader.Limits);
                var main = document.MainDocumentPart ?? throw new DomainException("The file could not be read as a Word document");
                var headingStyles = HeadingStyles(main);

                var paragraphs = new List<Paragraph>();
                var position = 0;
                foreach (var paragraph in main.Document?.Body?.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>() ?? [])
                {
                    position++;
                    var properties = paragraph.ParagraphProperties;
                    var style = properties?.ParagraphStyleId?.Val?.Value;
                    var isHeading = properties?.OutlineLevel is not null || (style is not null && headingStyles.Contains(style));
                    var isListItem = properties?.NumberingProperties is not null;
                    paragraphs.Add(new Paragraph(position, TextOf(paragraph), isHeading, isListItem));
                }

                return paragraphs;
            }
            catch (Exception error) when (error is DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or InvalidDataException
                                              or IOException or FormatException or System.Xml.XmlException or InvalidOperationException)
            {
                throw new DomainException("The file could not be read as a Word document");
            }
        }

        // The text a reader sees: text runs, tabs and line breaks. Deleted text (w:delText) is a different element.
        private static string TextOf(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
            => string.Concat(paragraph.Descendants().Select(e => e switch
            {
                Text text => text.Text,
                TabChar => " ",
                Break => "\n",
                _ => ""
            })).Trim();

        // Styles that are headings: Word stores the built-in names in English ("heading 1", "Title") whatever the
        // language of the user's Word, and gives them an outline level. Without a styles part, the id is used.
        private static HashSet<string> HeadingStyles(MainDocumentPart main)
        {
            var styles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var style in main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
            {
                var name = style.StyleName?.Val?.Value ?? "";
                if (style.StyleId?.Value is { } id
                    && (name.StartsWith("heading ", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("Title", StringComparison.OrdinalIgnoreCase)
                        || style.StyleParagraphProperties?.OutlineLevel is not null))
                {
                    styles.Add(id);
                }
            }

            for (var level = 1; level <= 9; level++)
            {
                styles.Add($"Heading{level}");
                styles.Add($"Overskrift{level}");
            }

            styles.Add("Title");
            styles.Add("Titel");
            return styles;
        }
    }
}
