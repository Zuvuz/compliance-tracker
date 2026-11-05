using ComplianceTracker.Application.Reports;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-056: writes a generated document as DOCX, so people can finish it in Word or LibreOffice.
    // The headings use Word's built-in style ids (Title, Heading1, Heading2), so the navigation pane and a table of
    // contents work. "To be completed" is italic and highlighted, so it is easy to find.
    public class OpenXmlDocumentWriter : IDocumentWriter
    {
        public byte[] ToDocx(GeneratedDocument document)
        {
            using var stream = new MemoryStream();
            using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = word.AddMainDocumentPart();
                AddStyles(main);

                var body = new Body();
                foreach (var block in document.Blocks)
                {
                    body.Append(Paragraph(block));
                }

                main.Document = new Document(body);
                main.Document.Save();
            }

            return stream.ToArray();
        }

        private static Paragraph Paragraph(DocumentBlock block)
        {
            var run = new Run(new Text(block.Kind == DocumentBlockKind.Bullet ? "• " + block.Text : block.Text) { Space = SpaceProcessingModeValues.Preserve });
            var properties = new ParagraphProperties();

            switch (block.Kind)
            {
                case DocumentBlockKind.Title:
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = "Title" };
                    break;
                case DocumentBlockKind.Heading1:
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = "Heading1" };
                    break;
                case DocumentBlockKind.Heading2:
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = "Heading2" };
                    break;
                case DocumentBlockKind.Bullet:
                    properties.Indentation = new Indentation { Left = "360", Hanging = "240" };
                    break;
                case DocumentBlockKind.ToBeCompleted:
                    run.RunProperties = new RunProperties(new Italic(), new Highlight { Val = HighlightColorValues.Yellow });
                    break;
            }

            return new Paragraph(properties, run);
        }

        private static void AddStyles(MainDocumentPart main)
        {
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                    new FontSize { Val = "22" }))),
                Style("Normal", "Normal", null, false, null),
                Style("Title", "Title", "Normal", true, "40"),
                Style("Heading1", "heading 1", "Normal", true, "30"),
                Style("Heading2", "heading 2", "Normal", true, "24"));
            styles.Styles.Save();
        }

        private static Style Style(string id, string name, string? basedOn, bool bold, string? size)
        {
            var style = new Style { Type = StyleValues.Paragraph, StyleId = id, Default = id == "Normal" ? true : null };
            style.Append(new StyleName { Val = name });
            if (basedOn is not null)
            {
                style.Append(new BasedOn { Val = basedOn });
                style.Append(new NextParagraphStyle { Val = "Normal" });
                style.Append(new PrimaryStyle());
            }

            if (id.StartsWith("Heading", StringComparison.Ordinal))
            {
                style.Append(new StyleParagraphProperties(
                    new SpacingBetweenLines { Before = "240", After = "120" },
                    new OutlineLevel { Val = id == "Heading1" ? 0 : 1 }));
            }

            var run = new StyleRunProperties();
            if (bold)
            {
                run.Append(new Bold());
            }

            if (size is not null)
            {
                run.Append(new FontSize { Val = size });
            }

            style.Append(run);
            return style;
        }
    }
}
