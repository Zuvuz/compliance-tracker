using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // US-143 R7: which columns of an Excel or CSV file hold the code, the title and the text (and optionally the parent's
    // code and the kind), by the names in the header row. Remembered on the framework as one line of text.
    public sealed record TableColumns(string Code, string Title, string Text, string? Parent = null, string? Kind = null)
    {
        private const char Separator = '\u001F'; // never in a header the admin typed

        public string Serialize() => string.Join(Separator, Code, Title, Text, Parent ?? "", Kind ?? "");

        public static TableColumns? Parse(string? text)
        {
            var parts = (text ?? "").Split(Separator);
            if (parts.Length != 5 || parts[..3].Any(string.IsNullOrWhiteSpace))
            {
                return null;
            }

            return new TableColumns(parts[0], parts[1], parts[2], Blank(parts[3]), Blank(parts[4]));
        }

        // #52: the columns a file most likely uses, from the names in its header row (English and Danish), so "Read the
        // requirements" works at once for a usual file. Otherwise the first, second and third columns.
        public static TableColumns Guess(IReadOnlyList<string> headers)
        {
            string? Find(params string[] names)
                => headers.FirstOrDefault(h => names.Any(n => string.Equals(h.Trim(), n, StringComparison.OrdinalIgnoreCase)))
                   ?? headers.FirstOrDefault(h => names.Any(n => h.Trim().StartsWith(n, StringComparison.OrdinalIgnoreCase)));

            var code = Find("code", "id", "ref", "reference", "control", "clause", "krav-id", "kode", "nr", "no");
            var title = Find("title", "name", "heading", "titel", "navn", "overskrift");
            var text = Find("text", "description", "requirement", "content", "tekst", "beskrivelse", "krav");
            var parent = Find("parent", "parent code", "forælder");
            var kind = Find("kind", "type", "art");

            var rest = headers.Where(h => h != code && h != title && h != text).ToList();
            code ??= rest.Count > 0 ? rest[0] : headers.FirstOrDefault() ?? "";
            rest.Remove(code);
            title ??= rest.Count > 0 ? rest[0] : code;
            rest.Remove(title);
            text ??= rest.Count > 0 ? rest[0] : title;
            return new TableColumns(code, title, text,
                parent is not null && parent != code && parent != title && parent != text ? parent : null,
                kind is not null && kind != code && kind != title && kind != text ? kind : null);
        }

        // The form's choice; the code, title and text must be three columns of the file
        public static TableColumns Choose(IReadOnlyList<string> headers, string? code, string? title, string? text, string? parent, string? kind)
        {
            string? Pick(string? name) => headers.FirstOrDefault(h => string.Equals(h.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase));
            var (c, t, x) = (Pick(code), Pick(title), Pick(text));
            if (c is null || t is null || x is null)
            {
                throw new DomainException("Choose the columns for the code, the title and the text");
            }

            return new TableColumns(c, t, x, Pick(parent), Pick(kind));
        }

        private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // US-143 R7, R8: reads the rows of an Excel sheet or a CSV file into requirements with the chosen columns. The first row
    // is the header. A row without a code is skipped; a code twice keeps the first row and is listed in the check.
    public static class TableParser
    {
        public static ExtractionResult Parse(IReadOnlyList<IReadOnlyList<string>> rows, TableColumns columns)
        {
            if (rows.Count == 0)
            {
                return ExtractionResult.Empty;
            }

            var header = rows[0].Select(h => h.Trim()).ToList();
            int Index(string? name) => name is null ? -1 : header.FindIndex(h => string.Equals(h, name.Trim(), StringComparison.OrdinalIgnoreCase));
            var (code, title, text, parent, kind) = (Index(columns.Code), Index(columns.Title), Index(columns.Text), Index(columns.Parent), Index(columns.Kind));
            foreach (var (name, index) in new[] { (columns.Code, code), (columns.Title, title), (columns.Text, text) })
            {
                if (index < 0)
                {
                    throw new DomainException($"The column \"{name}\" is not in the file");
                }
            }

            string Cell(IReadOnlyList<string> row, int index) => index >= 0 && index < row.Count ? (row[index] ?? "").Trim() : "";

            var found = new List<ExtractedRequirement>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var twice = new List<string>();
            var emptyRows = 0;
            foreach (var row in rows.Skip(1))
            {
                var rowCode = Cell(row, code);
                if (rowCode.Length == 0)
                {
                    emptyRows += row.Any(c => !string.IsNullOrWhiteSpace(c)) ? 1 : 0;
                    continue;
                }

                if (rowCode.Length > 50 || !seen.Add(rowCode))
                {
                    if (rowCode.Length <= 50)
                    {
                        twice.Add(rowCode);
                    }

                    continue;
                }

                if (found.Count >= RequirementParser.MaxRequirements)
                {
                    break;
                }

                var rowTitle = Cell(row, title);
                var rowText = Cell(row, text);
                found.Add(new ExtractedRequirement(
                    rowCode,
                    Cut(rowTitle.Length > 0 ? rowTitle : rowCode, RequirementParser.MaxTitleLength),
                    Cut(rowText, RequirementParser.MaxDescriptionLength),
                    KindOf(Cell(row, kind)) ?? RequirementParser.Classify(rowTitle + " " + rowText, qualityModel: false),
                    Cell(row, parent) is { Length: > 0 } p ? p : null));
            }

            var codes = found.Select(f => f.Code).ToList();
            var check = new ExtractionCheck(
                found.Count, 1, found.Count == 0 ? 1 : 0, 100,
                emptyRows > 0 ? [new LeftOverText(1, $"{emptyRows} row{(emptyRows == 1 ? "" : "s")} without a code")] : [],
                [], RequirementParser.Gaps(codes), twice.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), [], codes);
            return new ExtractionResult(found, check);
        }

        // "Requirement", "shall", "Recommendation", "should", "Characteristic"; null for anything else (then the text decides)
        public static RequirementKind? KindOf(string value) => value.Trim().ToLowerInvariant() switch
        {
            "requirement" or "shall" or "mandatory" => RequirementKind.Requirement,
            "recommendation" or "recommended" or "should" => RequirementKind.Recommendation,
            "characteristic" or "subcharacteristic" => RequirementKind.Characteristic,
            _ => null
        };

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }
}
