using System.Text;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Domain.Mappings
{
    // US-149 R1: the diagram formats the tracker reads. Each is text or XML; nothing in the file is run (R2).
    public enum DiagramFormat
    {
        Drawio,
        Mermaid,
        PlantUml,
        Bpmn,
        Svg
    }

    // A box, actor, task, state or text in a diagram
    public sealed record DiagramElement(string Name, string Kind);

    // An arrow between two elements, with its label when it has one
    public sealed record DiagramLink(string From, string To, string? Label)
    {
        public override string ToString() => Label is null ? $"{From} → {To}" : $"{From} → {To}: {Label}";
    }

    // What a diagram says: its elements and links, cleaned and limited (R5). TotalElements counts what the file had.
    public sealed partial record DiagramContent(DiagramFormat Format, IReadOnlyList<DiagramElement> Elements, IReadOnlyList<DiagramLink> Links, int TotalElements)
    {
        public const int MaxElements = 2000;
        public const int MaxLinks = 2000;
        public const int MaxNameLength = 200;

        public static readonly IReadOnlyDictionary<string, DiagramFormat> ContentTypes = new Dictionary<string, DiagramFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/vnd.jgraph.mxfile"] = DiagramFormat.Drawio,
            ["text/vnd.mermaid"] = DiagramFormat.Mermaid,
            ["text/vnd.plantuml"] = DiagramFormat.PlantUml,
            ["application/bpmn+xml"] = DiagramFormat.Bpmn,
            ["image/svg+xml"] = DiagramFormat.Svg
        };

        public static DiagramFormat? FormatOf(string? contentType)
            => contentType is not null && ContentTypes.TryGetValue(contentType, out var format) ? format : null;

        public bool IsCut => TotalElements > Elements.Count;

        // e.g. "Diagram: 3 elements, 2 links"
        public string Summary => $"Diagram: {Count(Elements.Count, "element")}, {Count(Links.Count, "link")}";

        // R5: e.g. "Read the first 2,000 of 2,500 elements"
        public string? CutNote => IsCut ? $"Read the first {Elements.Count:N0} of {TotalElements:N0} elements" : null;

        // R3: what the AI, the evidence check and search read: the elements, then the links
        public string Text()
        {
            var text = new StringBuilder();
            if (Elements.Count > 0)
            {
                text.AppendLine("Elements: " + string.Join("; ", Elements.Select(e => e.Name)));
            }

            foreach (var link in Links)
            {
                text.AppendLine(link.ToString());
            }

            return text.ToString().TrimEnd();
        }

        // Cleans the names (no markup, one line, at most 200 characters), drops empty and repeated elements, and keeps
        // at most 2,000 elements and links
        public static DiagramContent Of(DiagramFormat format, IEnumerable<DiagramElement> elements, IEnumerable<DiagramLink> links)
        {
            var cleaned = elements
                .Select(e => e with { Name = Clean(e.Name), Kind = Clean(e.Kind) })
                .Where(e => e.Name.Length > 0)
                .DistinctBy(e => (e.Name, e.Kind))
                .ToList();
            var arrows = links
                .Select(l => new DiagramLink(Clean(l.From), Clean(l.To), Clean(l.Label) is { Length: > 0 } label ? label : null))
                .Where(l => l.From.Length > 0 && l.To.Length > 0)
                .Distinct()
                .Take(MaxLinks)
                .ToList();
            return new DiagramContent(format, cleaned.Take(MaxElements).ToList(), arrows, cleaned.Count);
        }

        // HTML tags out, entities decoded, white space (also line breaks) to one space
        public static string Clean(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            var plain = System.Net.WebUtility.HtmlDecode(Tags().Replace(text.Replace("<br>", " ").Replace("\\n", " "), " "));
            plain = Spaces().Replace(plain, " ").Trim();
            return plain.Length <= MaxNameLength ? plain : plain[..MaxNameLength];
        }

        private static string Count(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

        [GeneratedRegex("<[^>]*>")]
        private static partial Regex Tags();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();
    }
}
