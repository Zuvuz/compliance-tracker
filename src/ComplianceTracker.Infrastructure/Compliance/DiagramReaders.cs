using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Infrastructure.Compliance
{
    // US-149 R2: XML is read with DTDs and external entities switched off, so a file cannot reach outside or blow up
    internal static class SafeXml
    {
        public static XDocument Load(byte[] content)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 20_000_000,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            try
            {
                using var reader = XmlReader.Create(new MemoryStream(content), settings);
                return XDocument.Load(reader);
            }
            catch (XmlException error) when (error.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase))
            {
                throw new DiagramUnreadableException("the file has a DTD, which is not allowed", error);
            }
            catch (XmlException error)
            {
                throw new DiagramUnreadableException("the XML is not well-formed", error);
            }
        }

        public static XDocument Parse(string xml) => Load(Encoding.UTF8.GetBytes(xml));
    }

    // draw.io / diagrams.net: <mxfile> with one <diagram> per page, either as XML or compressed (deflate + base64)
    public partial class DrawioDiagramReader : IDiagramReader
    {
        public DiagramFormat Format => DiagramFormat.Drawio;

        public DiagramContent Read(byte[] content) => Read(SafeXml.Load(content));

        public static DiagramContent Read(XDocument document)
        {
            var models = new List<XElement>();
            var root = document.Root ?? throw new DiagramUnreadableException("no diagram was found in the file");
            if (root.Name.LocalName == "mxGraphModel")
            {
                models.Add(root);
            }

            foreach (var page in root.Descendants().Where(d => d.Name.LocalName == "diagram"))
            {
                if (page.Elements().FirstOrDefault(e => e.Name.LocalName == "mxGraphModel") is { } model)
                {
                    models.Add(model);
                }
                else if (!string.IsNullOrWhiteSpace(page.Value))
                {
                    models.Add(SafeXml.Parse(Unpack(page.Value.Trim())).Root!);
                }
            }

            if (models.Count == 0)
            {
                throw new DiagramUnreadableException("no diagram was found in the file");
            }

            var elements = new List<DiagramElement>();
            var links = new List<DiagramLink>();
            foreach (var model in models)
            {
                Read(model, elements, links);
            }

            return DiagramContent.Of(DiagramFormat.Drawio, elements, links);
        }

        private static void Read(XElement model, List<DiagramElement> elements, List<DiagramLink> links)
        {
            // A cell may sit in an <object> or <UserObject> that holds its id and label
            var cells = model.Descendants().Where(d => d.Name.LocalName == "mxCell")
                .Select(c => (Cell: c, Owner: c.Parent is { } p && p.Name.LocalName is "object" or "UserObject" ? p : null))
                .Select(x => (
                    x.Cell,
                    Id: (string?)x.Owner?.Attribute("id") ?? (string?)x.Cell.Attribute("id") ?? "",
                    Label: DiagramContent.Clean((string?)x.Owner?.Attribute("label") ?? (string?)x.Cell.Attribute("value")),
                    Style: (string?)x.Cell.Attribute("style") ?? ""))
                .ToList();
            var edges = cells.Where(c => (string?)c.Cell.Attribute("edge") == "1").ToDictionary(c => c.Id, c => c);
            var names = cells.Where(c => c.Label.Length > 0).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Label);

            // A label that belongs to an arrow is a vertex whose parent is the arrow
            var edgeLabels = cells
                .Where(c => (string?)c.Cell.Attribute("vertex") == "1" && edges.ContainsKey((string?)c.Cell.Attribute("parent") ?? ""))
                .GroupBy(c => (string)c.Cell.Attribute("parent")!)
                .ToDictionary(g => g.Key, g => string.Join(" ", g.Select(c => c.Label).Where(l => l.Length > 0)));

            foreach (var vertex in cells.Where(c => (string?)c.Cell.Attribute("vertex") == "1" && !edges.ContainsKey((string?)c.Cell.Attribute("parent") ?? "")))
            {
                elements.Add(new DiagramElement(vertex.Label, Kind(vertex.Style)));
            }

            foreach (var edge in edges.Values)
            {
                if (names.TryGetValue((string?)edge.Cell.Attribute("source") ?? "", out var from)
                    && names.TryGetValue((string?)edge.Cell.Attribute("target") ?? "", out var to))
                {
                    var label = edge.Label.Length > 0 ? edge.Label : edgeLabels.GetValueOrDefault(edge.Id);
                    links.Add(new DiagramLink(from, to, label));
                }
            }
        }

        private static string Kind(string style) => style switch
        {
            _ when style.Contains("cylinder", StringComparison.OrdinalIgnoreCase) => "database",
            _ when style.Contains("umlActor", StringComparison.OrdinalIgnoreCase) => "actor",
            _ when style.Contains("swimlane", StringComparison.OrdinalIgnoreCase) || style.Contains("group", StringComparison.OrdinalIgnoreCase) => "group",
            _ when style.StartsWith("text", StringComparison.OrdinalIgnoreCase) => "text",
            _ when style.Contains("ellipse", StringComparison.OrdinalIgnoreCase) => "ellipse",
            _ when style.Contains("rhombus", StringComparison.OrdinalIgnoreCase) => "decision",
            _ => "shape"
        };

        // A compressed page: base64 → raw deflate → URL-encoded XML
        private static string Unpack(string packed)
        {
            try
            {
                using var input = new MemoryStream(Convert.FromBase64String(packed));
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var limited = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    limited.Write(buffer, 0, read);
                    if (limited.Length > 20_000_000)
                    {
                        throw new DiagramUnreadableException("the compressed draw.io page is too large");
                    }
                }

                return Uri.UnescapeDataString(Encoding.UTF8.GetString(limited.ToArray()));
            }
            catch (Exception error) when (error is FormatException or InvalidDataException)
            {
                throw new DiagramUnreadableException("the compressed draw.io page could not be unpacked", error);
            }
        }
    }

    // Mermaid: flowcharts, sequence, state and class diagrams. Only node shapes, arrows and their labels are read.
    public partial class MermaidDiagramReader : IDiagramReader
    {
        public DiagramFormat Format => DiagramFormat.Mermaid;

        // Lines that only style or group a diagram, by their first word
        private static readonly HashSet<string> Skipped = ["style", "classDef", "class", "click", "linkStyle", "direction", "end", "note", "Note", "autonumber", "activate", "deactivate", "loop", "alt", "else", "opt", "par", "and", "rect", "critical", "break"];

        public DiagramContent Read(byte[] content)
        {
            var lines = Encoding.UTF8.GetString(content).Replace("\r", "").Split('\n')
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("%%", StringComparison.Ordinal)).ToList();
            if (lines.Count == 0)
            {
                throw new DiagramUnreadableException("the diagram is empty");
            }

            var kind = lines[0].Split(' ')[0] switch
            {
                "sequenceDiagram" => "participant",
                "stateDiagram" or "stateDiagram-v2" => "state",
                "classDiagram" => "class",
                "erDiagram" => "entity",
                _ => "node"
            };

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var order = new List<(string Id, string Kind)>();
            var links = new List<(string From, string To, string? Label)>();

            void Node(string id, string? label, string nodeKind)
            {
                if (label is not null)
                {
                    names[id] = label;
                }

                if (!order.Any(o => o.Id == id))
                {
                    order.Add((id, nodeKind));
                }
            }

            foreach (var line in lines.Skip(1))
            {
                if (Skipped.Contains(line.Split(' ', 2)[0]))
                {
                    continue;
                }

                if (line.StartsWith("subgraph ", StringComparison.Ordinal))
                {
                    var (gid, glabel) = Ref(line["subgraph ".Length..]);
                    Node(gid, glabel, "group");
                    continue;
                }

                if (Participant().Match(line) is { Success: true } participant)
                {
                    Node(participant.Groups["id"].Value, participant.Groups["label"].Success ? participant.Groups["label"].Value.Trim() : null, "participant");
                    continue;
                }

                // A trailing ": label" (sequence and state diagrams); not inside a node's brackets
                var (body, tail) = SplitTail(line);

                var arrows = Arrow().Matches(body);
                if (arrows.Count == 0)
                {
                    var (nid, nlabel) = Ref(body);
                    if (nid.Length > 0 && Regex.IsMatch(nid, @"^[\w.\-\[\]*]+$"))
                    {
                        Node(nid, nlabel, kind);
                    }

                    continue;
                }

                var segments = new List<string>();
                var start = 0;
                foreach (Match arrow in arrows)
                {
                    segments.Add(body[start..arrow.Index]);
                    start = arrow.Index + arrow.Length;
                }

                segments.Add(body[start..]);
                var refs = segments.Select(Ref).ToList();
                foreach (var (id, label) in refs)
                {
                    Node(id, label, kind);
                }

                for (var i = 0; i < arrows.Count; i++)
                {
                    var label = arrows[i].Groups["label"].Success ? arrows[i].Groups["label"].Value
                        : arrows[i].Groups["text"].Success ? arrows[i].Groups["text"].Value
                        : i == arrows.Count - 1 ? tail : null;
                    links.Add((refs[i].Id, refs[i + 1].Id, label));
                }
            }

            string Name(string id) => names.GetValueOrDefault(id, id);
            return DiagramContent.Of(DiagramFormat.Mermaid,
                order.Select(o => new DiagramElement(Name(o.Id), o.Kind)),
                links.Select(l => new DiagramLink(Name(l.From), Name(l.To), l.Label)));
        }

        // "A[Web app]", "B[(Database)]", "C((Circle))", "D{Decision}", "E" → (id, label)
        private static (string Id, string? Label) Ref(string text)
        {
            var trimmed = text.Trim().TrimEnd(';').Trim();
            if (Shape().Match(trimmed) is { Success: true } shape)
            {
                return (shape.Groups["id"].Value, shape.Groups["label"].Value.Trim().Trim('"'));
            }

            var id = trimmed.Split(':', 2)[0].Split(' ', 2)[0].Trim();
            return (id, null);
        }

        [GeneratedRegex(@"^(?<id>[\w.\-]+)\s*(?:\[\[|\[\(|\(\(\(|\(\(|\(\[|\[/|\[\\|\[|\(|\{\{|\{|>)(?<label>.*?)(?:\]\]|\)\]|\)\)\)|\)\)|\]\)|/\]|\\\]|\]|\)|\}\}|\})(?::::\w+)?$")]
        private static partial Regex Shape();

        [GeneratedRegex(@"\s*(?:--\s+(?<text>[^\-][^>]*?)\s+)?(?<arrow><-->|-->>|->>|--\)|-\)|-\.->|-\.-|==>|===|-->|---|--x|--o|-x|->|<\|--|\*--|o--|\.\.>|\.\.)\s*(?:\|(?<label>[^|]*)\|)?\s*")]
        private static partial Regex Arrow();

        // "A --> B : label" → ("A --> B", "label"); a colon inside brackets or "|…|", or ":::class", is not a label
        private static (string Body, string? Tail) SplitTail(string line)
        {
            var depth = 0;
            var inPipe = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c is '[' or '(' or '{')
                {
                    depth++;
                }
                else if (c is ']' or ')' or '}')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (c == '|')
                {
                    inPipe = !inPipe;
                }
                else if (c == ':' && depth == 0 && !inPipe)
                {
                    if (i + 2 < line.Length && line[i + 1] == ':' && line[i + 2] == ':')
                    {
                        i += 2;
                        continue;
                    }

                    var tail = line[(i + 1)..].Trim();
                    return (line[..i].TrimEnd(), tail.Length > 0 ? tail : null);
                }
            }

            return (line, null);
        }

        [GeneratedRegex(@"^(?:participant|actor)\s+(?<id>[\w.\-]+)(?:\s+as\s+(?<label>.+))?$")]
        private static partial Regex Participant();
    }

    // PlantUML: the elements it declares (actor, usecase, component, database …), its shorthands and its arrows
    public partial class PlantUmlDiagramReader : IDiagramReader
    {
        public DiagramFormat Format => DiagramFormat.PlantUml;

        public DiagramContent Read(byte[] content)
        {
            var all = Encoding.UTF8.GetString(content).Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();
            var startAt = all.FindIndex(l => l.StartsWith("@start", StringComparison.OrdinalIgnoreCase));
            var endAt = all.FindIndex(l => l.StartsWith("@end", StringComparison.OrdinalIgnoreCase));
            var lines = all.Skip(startAt + 1).Take((endAt < 0 ? all.Count : endAt) - startAt - 1)
                .Where(l => l.Length > 0 && !l.StartsWith('\'')).ToList();
            if (lines.Count == 0)
            {
                throw new DiagramUnreadableException("the diagram is empty");
            }

            var aliases = new Dictionary<string, (string Name, string Kind)>(StringComparer.Ordinal);
            var elements = new List<DiagramElement>();
            var links = new List<DiagramLink>();

            void Declare(string name, string? alias, string kind)
            {
                elements.Add(new DiagramElement(name, kind));
                aliases[alias ?? name] = (name, kind);
                aliases[name] = (name, kind);
            }

            foreach (var line in lines)
            {
                if (Declaration().Match(line) is { Success: true } declaration)
                {
                    var (name, alias) = NameAndAlias(declaration.Groups["rest"].Value);
                    Declare(name, alias, declaration.Groups["kind"].Value.ToLowerInvariant());
                    continue;
                }

                if (Link().Match(line) is { Success: true } link && (link.Groups["arrow"].Value.Contains('-') || link.Groups["arrow"].Value.Contains('.')))
                {
                    var from = Endpoint(link.Groups["a"].Value);
                    var to = Endpoint(link.Groups["b"].Value);
                    foreach (var end in new[] { from, to })
                    {
                        if (!aliases.ContainsKey(end.Key))
                        {
                            Declare(end.Name, end.Key, end.Kind);
                        }
                    }

                    links.Add(new DiagramLink(aliases[from.Key].Name, aliases[to.Key].Name,
                        link.Groups["label"].Success ? link.Groups["label"].Value : null));
                    continue;
                }

                if (Shorthand().Match(line) is { Success: true } shorthand)
                {
                    var end = Endpoint(shorthand.Groups["shape"].Value);
                    Declare(end.Name, shorthand.Groups["alias"].Success ? shorthand.Groups["alias"].Value : end.Key, end.Kind);
                }
            }

            return DiagramContent.Of(DiagramFormat.PlantUml, elements, links);
        }

        // "\"Web app\" as W", "(Sign in) as UC1", "API <<service>> {", "Clinician" → (name, alias)
        private static (string Name, string? Alias) NameAndAlias(string rest)
        {
            var text = Stereotype().Replace(rest, "").TrimEnd('{', ' ').Trim();
            text = Regex.Replace(text, @"\s+#\w+$", "");
            var parts = Regex.Split(text, @"\s+as\s+");
            var first = Endpoint(parts[0]);
            if (parts.Length > 1)
            {
                var second = Endpoint(parts[1]);
                // "A as \"Long name\"" or "\"Long name\" as A": the quoted one is the name
                return parts[1].Trim().StartsWith('"') ? (second.Name, parts[0].Trim()) : (first.Name, parts[1].Trim());
            }

            return (first.Name, null);
        }

        // An end of an arrow, as written: its key (for aliases), its name and its kind
        private static (string Key, string Name, string Kind) Endpoint(string text)
        {
            var t = text.Trim();
            return t switch
            {
                _ when t.StartsWith('"') && t.EndsWith('"') && t.Length > 1 => (t, t[1..^1], "element"),
                _ when t.StartsWith('(') && t.EndsWith(')') => (t, t[1..^1], "usecase"),
                _ when t.StartsWith('[') && t.EndsWith(']') => (t, t[1..^1], "component"),
                _ when t.StartsWith(':') && t.EndsWith(':') && t.Length > 1 => (t, t[1..^1], "actor"),
                _ => (t, t, "element")
            };
        }

        [GeneratedRegex(@"^(?<kind>actor|participant|usecase|component|database|node|cloud|boundary|control|entity|collections|queue|rectangle|package|frame|folder|interface|class|state|agent|storage|card|file|artifact|person|system|object|abstract)\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
        private static partial Regex Declaration();

        [GeneratedRegex(@"^(?<a>""[^""]+""|\([^)]+\)|\[[^\]]+\]|:[^:]+:|[\w.]+)\s*(?<arrow>(?:<\|?|\*|o|\+|#|x|\})?[-.=]+(?:\[[^\]]*\])?(?:left|right|up|down|le|ri|u|d|l|r)?[-.=]*(?:\|?>|\*|o|\+|#|x|\{)?)\s*(?<b>""[^""]+""|\([^)]+\)|\[[^\]]+\]|:[^:]+:|[\w.]+)\s*(?::\s*(?<label>.*))?$")]
        private static partial Regex Link();

        [GeneratedRegex(@"^(?<shape>\([^)]+\)|\[[^\]]+\]|:[^:]+:)(?:\s+as\s+(?<alias>[\w.]+))?$")]
        private static partial Regex Shorthand();

        [GeneratedRegex(@"<<[^>]*>>")]
        private static partial Regex Stereotype();
    }

    // BPMN 2.0: the flow nodes of every process (tasks, events, gateways), the pools and lanes, and the flows between them
    public class BpmnDiagramReader : IDiagramReader
    {
        public DiagramFormat Format => DiagramFormat.Bpmn;

        private static readonly HashSet<string> Nodes =
        [
            "task", "userTask", "serviceTask", "sendTask", "receiveTask", "manualTask", "businessRuleTask", "scriptTask",
            "callActivity", "subProcess", "startEvent", "endEvent", "intermediateCatchEvent", "intermediateThrowEvent",
            "boundaryEvent", "exclusiveGateway", "parallelGateway", "inclusiveGateway", "eventBasedGateway", "complexGateway",
            "dataObjectReference", "dataStoreReference", "participant", "lane"
        ];

        public DiagramContent Read(byte[] content)
        {
            var document = SafeXml.Load(content);
            if (document.Root?.Name.LocalName != "definitions")
            {
                throw new DiagramUnreadableException("this is not a BPMN 2.0 file (no <definitions>)");
            }

            var nodes = document.Descendants().Where(d => Nodes.Contains(d.Name.LocalName)).ToList();
            var names = nodes.Where(n => n.Attribute("id") is not null)
                .GroupBy(n => (string)n.Attribute("id")!)
                .ToDictionary(g => g.Key, g => Name(g.First()));
            var elements = nodes.Select(n => new DiagramElement(Name(n), Kind(n.Name.LocalName)));
            var links = document.Descendants().Where(d => d.Name.LocalName is "sequenceFlow" or "messageFlow")
                .Where(f => names.ContainsKey((string?)f.Attribute("sourceRef") ?? "") && names.ContainsKey((string?)f.Attribute("targetRef") ?? ""))
                .Select(f => new DiagramLink(names[(string)f.Attribute("sourceRef")!], names[(string)f.Attribute("targetRef")!], (string?)f.Attribute("name")));
            return DiagramContent.Of(DiagramFormat.Bpmn, elements, links);
        }

        // An unnamed start or end event is called by what it is, e.g. "Start event"
        private static string Name(XElement node)
            => (string?)node.Attribute("name") is { Length: > 0 } name ? name : Words(node.Name.LocalName);

        private static string Kind(string localName) => localName switch
        {
            "participant" or "lane" => "group",
            _ when localName.EndsWith("Event", StringComparison.Ordinal) => "event",
            _ when localName.EndsWith("Gateway", StringComparison.Ordinal) => "gateway",
            _ when localName.StartsWith("data", StringComparison.Ordinal) => "data",
            _ => "task"
        };

        // "startEvent" → "Start event"
        private static string Words(string camel)
        {
            var spaced = Regex.Replace(camel, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
            return char.ToUpperInvariant(spaced[0]) + spaced[1..];
        }
    }

    // SVG: its text (also in draw.io's foreignObject labels). A draw.io export carries its whole diagram, which is read
    // instead. Scripts and styles are never read, let alone run (R2).
    public class SvgDiagramReader : IDiagramReader
    {
        public DiagramFormat Format => DiagramFormat.Svg;

        public DiagramContent Read(byte[] content)
        {
            var document = SafeXml.Load(content);
            var root = document.Root;
            if (root?.Name.LocalName != "svg")
            {
                throw new DiagramUnreadableException("this is not an SVG file (no <svg>)");
            }

            // draw.io puts the diagram itself in the content attribute
            if ((string?)root.Attribute("content") is { } embedded && embedded.Contains("<mxfile", StringComparison.Ordinal))
            {
                var drawio = DrawioDiagramReader.Read(SafeXml.Parse(embedded));
                return drawio with { Format = DiagramFormat.Svg };
            }

            var texts = root.Descendants()
                .Where(d => d.Name.LocalName is "text" or "foreignObject")
                .Where(d => !d.Ancestors().Any(a => a.Name.LocalName is "script" or "style" or "text" or "foreignObject"))
                .Select(d => string.Concat(d.DescendantNodes().OfType<XText>()
                    .Where(t => t.Parent?.Name.LocalName is not ("script" or "style"))
                    .Select(t => t.Value + " ")))
                .Select(t => new DiagramElement(t, "text"));
            return DiagramContent.Of(DiagramFormat.Svg, texts, []);
        }
    }
}
