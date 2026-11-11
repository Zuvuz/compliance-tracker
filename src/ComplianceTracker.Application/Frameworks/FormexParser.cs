using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // US-137 R1–R3: reads an EU act in Formex 4, the Publications Office's XML that EUR-Lex publishes, into requirements.
    // The structure comes from the XML, not from reading a PDF: articles (ARTICLE, TI.ART), paragraphs (PARAG, NO.PARAG),
    // points (LIST, ITEM, NP, NO.P) and annexes (ANNEX) with their parts (GR.SEQ). An obligation is split down to the finest
    // level that holds its own obligation (R2): a paragraph with points becomes its points. Every other part is left out
    // with a reason (R3): recitals, definitions, authorities' duties, deleted provisions and the final provisions.
    // Codes follow the PDF reader's: "Art. 14(2)(a)" and "Annex I Part I (2)(a)".
    public static partial class FormexParser
    {
        public const string Recital = "Recital";
        public const string Definition = "Definition";
        public const string AuthorityDuty = "An authority's duty";
        public const string Deleted = "Deleted";
        public const string FinalProvision = "Final provision";

        // "Article 14", "Article 14a"
        [GeneratedRegex(@"Article\s+(?<number>\d{1,3}[a-z]?)", RegexOptions.IgnoreCase)]
        private static partial Regex ArticleNumber();

        // "ANNEX I", "Annex IV"
        [GeneratedRegex(@"ANNEX\s+(?<number>[IVXLC]{1,6})\b", RegexOptions.IgnoreCase)]
        private static partial Regex AnnexNumber();

        // "Part I", "PART II"
        [GeneratedRegex(@"^Part\s+(?<number>[IVXLC]{1,6})\b", RegexOptions.IgnoreCase)]
        private static partial Regex PartNumber();

        // "1.", "(1)", "(a)", "(iv)", "1"
        [GeneratedRegex(@"^\(?(?<label>[0-9]{1,3}|[a-z]{1,4})\)?\.?$")]
        private static partial Regex Label();

        // R3: a paragraph whose subject is an authority, not the manufacturer: "Member States shall", "The Commission shall
        // adopt", "Notified bodies shall", "Market surveillance authorities may", "The Board shall"
        [GeneratedRegex(@"^(?:\d+\.\s*)?(?:The\s+)?(?:Member States?|Commission|notified bod(?:y|ies)|market surveillance authorit(?:y|ies)|national competent authorit(?:y|ies)|competent authorit(?:y|ies)|Board|AI Office|ENISA|CSIRTs?|Agency|European Data Protection Board|supervisory authorit(?:y|ies))\b[^.]{0,120}?\b(?:shall|may|is empowered|are empowered)\b", RegexOptions.IgnoreCase)]
        private static partial Regex AuthoritySubject();

        [GeneratedRegex(@"^\(?deleted\)?\.?$", RegexOptions.IgnoreCase)]
        private static partial Regex DeletedText();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();

        // The parts of an act: one or more Formex files (the act and its annexes), in any order
        public sealed record Result(ExtractionResult Extraction, CompletenessCheck Completeness);

        public static Result Parse(string source, IEnumerable<string> files)
        {
            var requirements = new List<ExtractedRequirement>();
            var leftOut = new List<LeftOutPart>();
            int articles = 0, annexes = 0, paragraphs = 0, points = 0;

            var documents = files.Select(Load).Where(d => d?.Root is not null).Select(d => d!).ToList();

            // The act first, then its annexes in their order
            foreach (var root in documents.Select(d => d.Root!).OrderBy(r => r.Name.LocalName == "ANNEX" ? 1 : 0).ThenBy(AnnexOrder))
            {
                foreach (var recital in root.Descendants("CONSID"))
                {
                    leftOut.Add(new LeftOutPart($"Recital {LabelOf(recital) ?? (leftOut.Count(p => p.Reason == Recital) + 1).ToString()}", Recital));
                }

                foreach (var article in root.Descendants("ARTICLE"))
                {
                    articles++;
                    ReadArticle(article, requirements, leftOut, ref paragraphs, ref points);
                }

                foreach (var annex in root.DescendantsAndSelf("ANNEX"))
                {
                    annexes++;
                    ReadAnnex(annex, requirements, leftOut, ref paragraphs, ref points);
                }
            }

            // A code twice (e.g. an annex repeated) keeps the first
            var unique = requirements.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(RequirementParser.MaxRequirements).ToList();
            var codes = unique.Select(r => r.Code).ToList();
            var twice = requirements.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var check = new ExtractionCheck(unique.Count, documents.Count, 0, 100, [], [], [], twice, [], codes);
            var completeness = new CompletenessCheck(source, articles, annexes, paragraphs, points, unique.Count,
                leftOut.Take(CompletenessCheck.MaxListed).ToList(), null, []);
            return new Result(new ExtractionResult(unique, check), completeness);
        }

        // ---------------------------------------------------------- Articles

        private static void ReadArticle(XElement article, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut, ref int paragraphs, ref int points)
        {
            var heading = Text(article.Element("TI.ART"));
            var number = ArticleNumber().Match(heading) is { Success: true } m ? m.Groups["number"].Value : heading;
            var code = $"Art. {number}";
            var title = Text(article.Element("STI.ART"));
            var reason = title switch
            {
                _ when title.Contains("Definition", StringComparison.OrdinalIgnoreCase) => Definition,
                _ when title.Contains("Entry into force", StringComparison.OrdinalIgnoreCase)
                       || title.Contains("Repeal", StringComparison.OrdinalIgnoreCase)
                       || title.Contains("Transitional provisions", StringComparison.OrdinalIgnoreCase)
                       || title.Contains("Amendment to", StringComparison.OrdinalIgnoreCase) => FinalProvision,
                _ => null
            };

            var parags = article.Elements("PARAG").ToList();
            if (parags.Count == 0)
            {
                // An article of one paragraph: its text, and its points if any
                ReadBlock(code, title.Length > 0 ? title : code, article, reason, requirements, leftOut, ref points);
                return;
            }

            foreach (var parag in parags)
            {
                paragraphs++;
                var label = Clean(Text(parag.Element("NO.PARAG")));
                ReadBlock($"{code}({label})", null, parag, reason, requirements, leftOut, ref points);
            }
        }

        // A paragraph (or an article without paragraphs): split into its points when it has any (R2), else one requirement.
        // The reason, when set, leaves the whole block out (R3).
        private static void ReadBlock(string code, string? title, XElement block, string? reason, List<ExtractedRequirement> requirements,
            List<LeftOutPart> leftOut, ref int points)
        {
            var own = OwnText(block);
            var items = block.Elements("ALINEA").SelectMany(a => a.Elements("LIST")).Concat(block.Elements("LIST")).SelectMany(l => l.Elements("ITEM")).ToList();
            var why = reason ?? (DeletedText().IsMatch(own) ? Deleted : AuthoritySubject().IsMatch(own) ? AuthorityDuty : null);

            if (items.Count == 0)
            {
                Add(code, title ?? Short(own), own, why, requirements, leftOut);
                return;
            }

            foreach (var item in items)
            {
                points++;
                ReadPoint(code, item, why, requirements, leftOut, ref points);
            }
        }

        private static void ReadPoint(string parent, XElement item, string? reason, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut, ref int points)
        {
            var np = item.Element("NP") ?? item;
            var label = Clean(Text(np.Element("NO.P")));
            var code = $"{parent}({label})";
            var subItems = np.Descendants("LIST").Take(1).SelectMany(l => l.Elements("ITEM")).ToList();
            var text = OwnText(np);
            if (subItems.Count == 0)
            {
                Add(code, Short(text), text, reason ?? (DeletedText().IsMatch(text) ? Deleted : null), requirements, leftOut);
                return;
            }

            foreach (var sub in subItems)
            {
                points++;
                ReadPoint(code, sub, reason, requirements, leftOut, ref points);
            }
        }

        // ---------------------------------------------------------- Annexes

        private static void ReadAnnex(XElement annex, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut, ref int paragraphs, ref int points)
        {
            var heading = Text(annex.Element("TITLE")?.Element("TI"));
            var number = AnnexNumber().Match(heading) is { Success: true } m ? m.Groups["number"].Value.ToUpperInvariant() : heading;
            var annexCode = $"Annex {number}";
            var contents = annex.Element("CONTENTS") ?? annex;
            var before = requirements.Count + leftOut.Count;
            ReadAnnexPart(annexCode, contents, requirements, leftOut, ref paragraphs, ref points);

            // An annex with no numbered parts (e.g. a list of products) is one requirement
            if (requirements.Count + leftOut.Count == before)
            {
                var text = OwnText(contents);
                Add(annexCode, Text(annex.Element("TITLE")?.Element("STI")) is { Length: > 0 } t ? t : annexCode, text, null, requirements, leftOut);
            }
        }

        private static void ReadAnnexPart(string code, XElement container, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut,
            ref int paragraphs, ref int points)
        {
            foreach (var child in container.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "GR.SEQ":
                        var title = Text(child.Element("TITLE")?.Element("TI"));
                        var part = PartNumber().Match(title) is { Success: true } p ? $"{code} Part {p.Groups["number"].Value.ToUpperInvariant()}" : code;
                        ReadAnnexPart(part, child, requirements, leftOut, ref paragraphs, ref points);
                        break;
                    case "NP":
                        ReadAnnexParagraph(code, child, requirements, leftOut, ref paragraphs, ref points);
                        break;
                    case "LIST":
                        foreach (var np in child.Elements("ITEM").Select(i => i.Element("NP")).OfType<XElement>())
                        {
                            ReadAnnexParagraph(code, np, requirements, leftOut, ref paragraphs, ref points);
                        }

                        break;
                    case "P" or "TITLE":
                        break;
                    default:
                        ReadAnnexPart(code, child, requirements, leftOut, ref paragraphs, ref points);
                        break;
                }
            }
        }

        // "(2) … products shall: (a) … (m) …": the paragraph, or its points when it has any (R2)
        private static void ReadAnnexParagraph(string code, XElement np, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut,
            ref int paragraphs, ref int points)
        {
            paragraphs++;
            var paragraphCode = $"{code} ({Clean(Text(np.Element("NO.P")))})";
            var items = np.Descendants("LIST").Take(1).SelectMany(l => l.Elements("ITEM")).ToList();
            if (items.Count == 0)
            {
                var text = OwnText(np);
                Add(paragraphCode, Short(text), text, DeletedText().IsMatch(text) ? Deleted : null, requirements, leftOut);
                return;
            }

            foreach (var item in items)
            {
                points++;
                ReadPoint(paragraphCode, item, null, requirements, leftOut, ref points);
            }
        }

        // ---------------------------------------------------------- Helpers

        private static void Add(string code, string title, string text, string? reason, List<ExtractedRequirement> requirements, List<LeftOutPart> leftOut)
        {
            if (reason is not null)
            {
                leftOut.Add(new LeftOutPart(code, reason));
                return;
            }

            requirements.Add(new ExtractedRequirement(
                code.Length <= 50 ? code : code[..50],
                Cut(title.Length > 0 ? title : code, RequirementParser.MaxTitleLength),
                Cut(text, RequirementParser.MaxDescriptionLength)));
        }

        // The text of an element without the text of its lists (its points are their own requirements)
        private static string OwnText(XElement element)
        {
            var copy = new XElement(element);
            copy.Descendants().Where(d => d.Name.LocalName is "LIST" or "NO.P" or "NO.PARAG" or "TI.ART" or "STI.ART" or "TITLE" or "NOTE").ToList().Remove();
            return Text(copy);
        }

        private static string Text(XElement? element) => element is null ? "" : Spaces().Replace(element.Value, " ").Trim();

        // "1." → "1", "(a)" → "a"
        private static string Clean(string label)
            => Label().Match(label.Trim()) is { Success: true } m ? m.Groups["label"].Value : label.Trim().Trim('(', ')', '.');

        private static string? LabelOf(XElement recital)
            => recital.Descendants("NO.P").FirstOrDefault() is { } no ? $"({Clean(Text(no))})" : null;

        private static int AnnexOrder(XElement root)
        {
            var heading = Text(root.Element("TITLE")?.Element("TI"));
            return AnnexNumber().Match(heading) is { Success: true } m ? Roman(m.Groups["number"].Value) : 0;
        }

        private static int Roman(string text)
        {
            var values = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100 };
            var total = 0;
            var upper = text.ToUpperInvariant();
            for (var i = 0; i < upper.Length; i++)
            {
                var value = values.GetValueOrDefault(upper[i]);
                total += i + 1 < upper.Length && values.GetValueOrDefault(upper[i + 1]) > value ? -value : value;
            }

            return total;
        }

        private static string Short(string text)
        {
            var cleaned = text.Trim();
            return cleaned.Length <= 120 ? cleaned : cleaned[..117].TrimEnd() + "…";
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];

        // ASVS V5.5.2: no DTDs or external entities, and a size limit, so a file cannot reach outside or blow up
        private static XDocument? Load(string xml)
        {
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    MaxCharactersInDocument = 50_000_000,
                    MaxCharactersFromEntities = 0
                };
                using var reader = XmlReader.Create(new StringReader(xml), settings);
                return XDocument.Load(reader);
            }
            catch (XmlException)
            {
                return null;
            }
        }
    }
}
