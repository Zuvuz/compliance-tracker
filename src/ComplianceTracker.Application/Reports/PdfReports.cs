namespace ComplianceTracker.Application.Reports
{
    // US-131 R3: what every page of a PDF report shows
    public record PdfHeader(string Organisation, string Project, string ReportName, DateTime MadeAtUtc, string MadeBy);

    // US-131: writes the reports as PDF (implemented in Infrastructure with PdfPig, Apache-2.0). The PDF is made from the
    // same report model as the Excel and Word files, so the content is the same in every format (R2).
    public interface IPdfWriter
    {
        // The traceability report (US-120): the summary, then each sheet as a table
        byte[] FromReport(ReportDocument document, PdfHeader header);

        // The technical documentation draft (US-056): its headings, paragraphs and bullets
        byte[] FromDocument(GeneratedDocument document, PdfHeader header);
    }

    // US-131: the text a standard PDF font can show (Windows-1252). Anything else becomes a close ASCII form or "?", so a
    // report never fails on an unusual character.
    public static class PdfText
    {
        private static readonly HashSet<char> Extra =
        [
            '€', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', 'Ž', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', 'ž', 'Ÿ'
        ];

        private static readonly Dictionary<char, string> Replacements = new()
        {
            ['→'] = "->",
            ['←'] = "<-",
            ['↔'] = "<->",
            ['↑'] = "Up",
            ['↓'] = "Down",
            ['✓'] = "v",
            ['✗'] = "x",
            ['≥'] = ">=",
            ['≤'] = "<=",
            ['−'] = "-",
            // US-054: Windows-1252 letters the standard PDF font has no glyph for; they would make the PDF fail
            ['\u00A0'] = " ",
            ['°'] = " deg",
            ['µ'] = "u",
            ['¾'] = "3/4",
            ['×'] = "x",
            ['÷'] = "/",
            ['€'] = "EUR",
            ['™'] = "(TM)",
            ['À'] = "A",
            ['É'] = "E",
            ['Ì'] = "I",
            ['Ñ'] = "N",
            ['Ò'] = "O",
            ['Ó'] = "O",
            ['Ô'] = "O",
            ['Õ'] = "O",
            ['Ö'] = "O",
            ['Ù'] = "U",
            ['Ú'] = "U",
            ['Û'] = "U",
            ['Ü'] = "U",
            ['Ý'] = "Y",
            ['Þ'] = "Th",
            ['à'] = "a",
            ['â'] = "a",
            ['ä'] = "a",
            ['å'] = "aa",
            ['ç'] = "c",
            ['ì'] = "i",
            ['í'] = "i",
            ['î'] = "i",
            ['ï'] = "i",
            ['ð'] = "d",
            ['ò'] = "o",
            ['ó'] = "o",
            ['ô'] = "o",
            ['ö'] = "o",
            ['ü'] = "u",
            ['ý'] = "y",
            ['þ'] = "th",
            ['ÿ'] = "y",
            ['Š'] = "S",
            ['š'] = "s",
            ['Ž'] = "Z",
            ['ž'] = "z",
            ['Ÿ'] = "Y",
            ['\t'] = "    "
        };

        public static bool CanShow(char c)
            => (c is >= ' ' and <= '~' || c is >= ' ' and <= 'ÿ' || Extra.Contains(c)) && !Replacements.ContainsKey(c);

        public static string Clean(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var builder = new System.Text.StringBuilder(text.Length);
            foreach (var c in text.Replace("\r\n", "\n"))
            {
                if (c == '\n' || CanShow(c))
                {
                    builder.Append(c);
                }
                else if (Replacements.TryGetValue(c, out var replacement))
                {
                    builder.Append(replacement);
                }
                else if (!char.IsControl(c) && !char.IsLowSurrogate(c))
                {
                    builder.Append('?');
                }
            }

            return builder.ToString();
        }

        // R4: splits text into lines no wider than the width, by words; a word longer than the line is split
        public static IReadOnlyList<string> Wrap(string text, double width, Func<string, double> measure)
        {
            var lines = new List<string>();
            foreach (var paragraph in Clean(text).Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (measure(candidate) <= width)
                    {
                        line = candidate;
                        continue;
                    }

                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }

                    // A word wider than the line is cut into pieces that fit
                    var rest = word;
                    while (rest.Length > 1 && measure(rest) > width)
                    {
                        var fit = 1;
                        while (fit < rest.Length && measure(rest[..(fit + 1)]) <= width)
                        {
                            fit++;
                        }

                        lines.Add(rest[..fit]);
                        rest = rest[fit..];
                    }

                    line = rest;
                }

                lines.Add(line);
            }

            return lines;
        }
    }
}
