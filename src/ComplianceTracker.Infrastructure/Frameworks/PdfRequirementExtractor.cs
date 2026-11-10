using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using TableColumns = ComplianceTracker.Application.Frameworks.TableColumns;

namespace ComplianceTracker.Infrastructure.Frameworks
{
    // Reads the text of an uploaded framework file; RequirementParser finds the requirements and what was left (US-007 R1,
    // US-070). US-143 R2: one reader per format, all giving lines (PDF, Word, HTML) or rows (Excel, CSV), so every format
    // goes through the same draft and checks (R8). No AI (domain model rule 9).
    // - PDF: PdfPig (Apache-2.0), page by page
    // - Word (.docx) and Excel (.xlsx): the OpenXml SDK (MIT)
    // - HTML saved from ISO's Online Browsing Platform: AngleSharp (MIT), one line per heading, paragraph, list item or
    //   table row; scripts are never run
    // - CSV: comma, semicolon or tab, with quotes
    public class PdfRequirementExtractor : IRequirementExtractor
    {
        // Limits, so a huge document cannot fill the database
        private const int MaxPages = 500;
        private const int MaxRows = 20000;

        public ExtractionResult Extract(CatalogFile file, TableColumns? columns = null)
        {
            if (file.Format == CatalogFormat.Pdf)
            {
                // R2: a scanned PDF is refused, so nobody believes it was read
                var pages = ReadPdf(file.Content);
                if (pages is { Count: > 0 } && pages.All(p => !p.Any(l => l.Any(char.IsLetter))))
                {
                    throw new DomainException(IRequirementExtractor.NoText);
                }

                return pages is null ? ExtractionResult.Empty : RequirementParser.Parse(pages);
            }

            try
            {
                return file.Format switch
                {
                    CatalogFormat.Word => RequirementParser.Parse([ReadWord(file.Content)]),
                    CatalogFormat.Html => RequirementParser.Parse([ReadHtml(file.Content)]),
                    _ when columns is null => ExtractionResult.Empty,
                    _ => TableParser.Parse(ReadTable(file), columns)
                };
            }
            catch (DomainException)
            {
                throw;
            }
            catch (Exception)
            {
                // A file that cannot be read is treated like one without requirements: the member adds them by hand
                return ExtractionResult.Empty;
            }
        }

        public IReadOnlyList<string> Headers(CatalogFile file)
        {
            if (file.Format is not (CatalogFormat.Excel or CatalogFormat.Csv))
            {
                return [];
            }

            try
            {
                return ReadTable(file).FirstOrDefault()?.Select(h => h.Trim()).Where(h => h.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            }
            catch (Exception)
            {
                return [];
            }
        }

        private static IReadOnlyList<IReadOnlyList<string>> ReadTable(CatalogFile file)
            => file.Format == CatalogFormat.Excel ? ReadExcel(file.Content) : ReadCsv(file.Content);

        // ---------------------------------------------------------- PDF

        // Null when PdfPig cannot read the file at all
        private static List<IReadOnlyList<string>>? ReadPdf(byte[] pdf)
        {
            try
            {
                using var document = PdfDocument.Open(pdf);
                return document.GetPages().Take(MaxPages)
                    .Select(page => (IReadOnlyList<string>)ContentOrderTextExtractor.GetText(page).Split('\n'))
                    .ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------- Word

        // One line per paragraph; a table row gives "code title" and then its other cells. Headings numbered by Word itself
        // (not typed) get their number back, so "Maintainability" under "4.2" reads "4.2.7 Maintainability".
        private static List<string> ReadWord(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                return [];
            }

            var numbered = NumberedHeadingStyles(document);
            var counters = new int[10];
            var lines = new List<string>();
            foreach (var element in body.Elements())
            {
                switch (element)
                {
                    case Word.Paragraph paragraph:
                        var text = Normalize(paragraph.InnerText);
                        if (text.Length == 0)
                        {
                            break;
                        }

                        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                        if (HeadingLevel(style) is { } level && !char.IsDigit(text[0])
                            && (paragraph.ParagraphProperties?.NumberingProperties is not null || numbered.Contains(style)))
                        {
                            counters[level]++;
                            Array.Clear(counters, level + 1, counters.Length - level - 1);
                            text = string.Join(".", counters.Skip(1).Take(level)) + " " + text;
                        }

                        lines.Add(text);
                        break;
                    case Word.Table table:
                        foreach (var row in table.Elements<Word.TableRow>())
                        {
                            lines.AddRange(RowLines(row.Elements<Word.TableCell>().Select(c => Normalize(c.InnerText)).ToList()));
                        }

                        break;
                }
            }

            return lines;
        }

        private static HashSet<string> NumberedHeadingStyles(WordprocessingDocument document)
            => document.MainDocumentPart?.StyleDefinitionsPart?.Styles?.Elements<Word.Style>()
                   .Where(s => s.StyleParagraphProperties?.NumberingProperties is not null && s.StyleId?.Value is not null)
                   .Select(s => s.StyleId!.Value!)
                   .ToHashSet() ?? [];

        // "Heading1" … "Heading6" (also "heading 1")
        private static int? HeadingLevel(string style)
        {
            var compact = style.Replace(" ", "");
            return compact.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) && int.TryParse(compact[7..], out var level) && level is >= 1 and <= 6
                ? level
                : null;
        }

        // ---------------------------------------------------------- HTML

        private static readonly HashSet<string> Blocks = ["H1", "H2", "H3", "H4", "H5", "H6", "P", "LI", "DT", "DD", "CAPTION", "PRE", "BLOCKQUOTE", "DIV", "SECTION"];
        private static readonly HashSet<string> Skipped = ["SCRIPT", "STYLE", "NOSCRIPT", "TEMPLATE", "NAV", "HEADER", "FOOTER", "BUTTON", "SELECT", "SVG"];
        private const string InnerBlocks = "h1,h2,h3,h4,h5,h6,p,li,dt,dd,div,table,tr,section,article,ul,ol,dl";

        private static List<string> ReadHtml(byte[] content)
        {
            var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(content));
            var lines = new List<string>();
            if (document.Body is { } body)
            {
                Walk(body, lines);
            }

            return lines;
        }

        private static void Walk(IElement element, List<string> lines)
        {
            if (Skipped.Contains(element.TagName))
            {
                return;
            }

            if (element.TagName == "TR")
            {
                lines.AddRange(RowLines(element.Children.Where(c => c.TagName is "TD" or "TH").Select(c => Normalize(c.TextContent)).ToList()));
                return;
            }

            // A block with no blocks inside is one line
            if (Blocks.Contains(element.TagName) && element.QuerySelectorAll(InnerBlocks).Length == 0)
            {
                var text = Normalize(element.TextContent);
                if (text.Length > 0)
                {
                    lines.Add(text);
                }

                return;
            }

            foreach (var child in element.Children)
            {
                Walk(child, lines);
            }
        }

        // ---------------------------------------------------------- Excel and CSV

        private static List<IReadOnlyList<string>> ReadExcel(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbook = document.WorkbookPart!;
            var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(i => i.InnerText).ToList() ?? [];

            // The first sheet in the workbook's order
            var first = workbook.Workbook?.Descendants<Sheet>().FirstOrDefault();
            if (first?.Id?.Value is not { } id || workbook.GetPartById(id) is not WorksheetPart sheet || sheet.Worksheet is null)
            {
                return [];
            }

            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in sheet.Worksheet.Descendants<Row>().Take(MaxRows))
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = ColumnIndex(cell.CellReference?.Value) ?? cells.Count;
                    while (cells.Count < column)
                    {
                        cells.Add("");
                    }

                    cells.Add(CellText(cell, shared));
                }

                rows.Add(cells);
            }

            return rows;
        }

        private static string CellText(Cell cell, List<string> shared)
        {
            if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(cell.CellValue?.Text, out var index))
            {
                return index >= 0 && index < shared.Count ? shared[index] : "";
            }

            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? "";
            }

            return cell.CellValue?.Text ?? "";
        }

        // "C7" → 2
        private static int? ColumnIndex(string? reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }

            var index = 0;
            foreach (var c in reference.TakeWhile(char.IsLetter))
            {
                index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }

            return index == 0 ? null : index - 1;
        }

        // R7: comma, semicolon or tab, whichever the header row has most of outside quotes; quotes may hold any of them,
        // line breaks and doubled quotes
        public static List<IReadOnlyList<string>> ReadCsv(byte[] content)
        {
            var text = new UTF8Encoding(false, false).GetString(content).TrimStart('﻿');
            var firstLine = text.Split('\n')[0];
            var separator = new[] { ';', ',', '\t' }.OrderByDescending(s => OutsideQuotes(firstLine, s)).First();

            var rows = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length && rows.Count < MaxRows; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else if (c == '"' && cell.Length == 0)
                {
                    quoted = true;
                }
                else if (c == separator)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString().TrimEnd('\r'));
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString().TrimEnd('\r'));
                rows.Add(row);
            }

            return rows;
        }

        private static int OutsideQuotes(string line, char separator)
        {
            var (count, quoted) = (0, false);
            foreach (var c in line)
            {
                quoted ^= c == '"';
                count += !quoted && c == separator ? 1 : 0;
            }

            return count;
        }

        // ---------------------------------------------------------- Shared

        // A table row as lines: the first two cells as "code title", then each other cell; an empty row gives nothing
        private static IEnumerable<string> RowLines(List<string> cells)
        {
            var filled = cells.Where(c => c.Length > 0).ToList();
            if (filled.Count == 0)
            {
                yield break;
            }

            yield return filled.Count >= 2 ? filled[0] + " " + filled[1] : filled[0];
            foreach (var rest in filled.Skip(2))
            {
                yield return rest;
            }
        }

        private static string Normalize(string? text)
            => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
