using System.Globalization;
using System.Text;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ComplianceTracker.Infrastructure.Risks
{
    // US-049 R9: a CSV file with ";" (Danish Excel) or ",". The separator is the one the header row uses most, outside
    // quotes. Quoted cells may hold separators, quotes ("") and line breaks.
    public class CsvRiskSheetReader : IRiskSheetReader
    {
        public bool CanRead(string fileName, byte[] content)
            => Path.GetExtension(fileName ?? "").Equals(".csv", StringComparison.OrdinalIgnoreCase)
               || Path.GetExtension(fileName ?? "").Equals(".txt", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<IReadOnlyList<string>> Read(byte[] content)
        {
            string text;
            try
            {
                text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
            }
            catch (DecoderFallbackException)
            {
                // Excel's "CSV" without UTF-8 is Windows-1252; Latin-1 reads the Danish letters the same
                text = Encoding.Latin1.GetString(content);
            }

            text = text.TrimStart('﻿');
            if (text.Contains('\0'))
            {
                throw new DomainException("The file is not a text file");
            }

            return Parse(text, Separator(text));
        }

        // The separator the first line uses most, outside quotes; ";" when it is a tie
        public static char Separator(string text)
        {
            int semicolons = 0, commas = 0;
            var quoted = false;
            foreach (var c in text)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && c is '\n' or '\r')
                {
                    break;
                }
                else if (!quoted && c == ';')
                {
                    semicolons++;
                }
                else if (!quoted && c == ',')
                {
                    commas++;
                }
            }

            return commas > semicolons ? ',' : ';';
        }

        public static IReadOnlyList<IReadOnlyList<string>> Parse(string text, char separator)
        {
            var rows = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
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
                else if (c is '\r' or '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(cell.ToString());
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
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }

    // US-049 R9: the first sheet of an Excel workbook (.xlsx). A date cell is a number of days; the rules read it as a date.
    public class XlsxRiskSheetReader : IRiskSheetReader
    {
        public bool CanRead(string fileName, byte[] content)
            => Path.GetExtension(fileName ?? "").Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
               && content.Length > 4 && content[0] == 'P' && content[1] == 'K';

        public IReadOnlyList<IReadOnlyList<string>> Read(byte[] content)
        {
            try
            {
                using var stream = new MemoryStream(content, writable: false);
                using var document = SpreadsheetDocument.Open(stream, false);
                var workbook = document.WorkbookPart ?? throw new DomainException("The workbook has no sheets");
                var first = workbook.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
                    ?? throw new DomainException("The workbook has no sheets");
                var sheet = (WorksheetPart)workbook.GetPartById(first.Id!.Value!);
                var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(s => s.InnerText).ToList() ?? [];

                var rows = new List<IReadOnlyList<string>>();
                foreach (var row in (sheet.Worksheet?.Descendants<Row>() ?? []).Take(RiskImportLimits.MaxSheetRows))
                {
                    var cells = new List<string>();
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var column = Column(cell.CellReference?.Value);
                        while (column >= 0 && cells.Count < column)
                        {
                            cells.Add(""); // an empty cell is not written in the file
                        }

                        cells.Add(Value(cell, shared));
                    }

                    rows.Add(cells);
                }

                return rows;
            }
            catch (Exception error) when (error is not DomainException and (DocumentFormat.OpenXml.Packaging.OpenXmlPackageException
                                              or InvalidDataException or System.Xml.XmlException or FormatException))
            {
                throw new DomainException("The workbook cannot be read");
            }
        }

        private static string Value(Cell cell, IReadOnlyList<string> shared)
        {
            var raw = cell.CellValue?.Text ?? cell.InnerText;
            if (cell.DataType?.Value == CellValues.SharedString
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < shared.Count)
            {
                return shared[index];
            }

            return cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : raw ?? "";
        }

        // "C7" → 2
        private static int Column(string? reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return -1;
            }

            var column = 0;
            foreach (var c in reference.TakeWhile(char.IsLetter))
            {
                column = column * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }

            return column - 1;
        }
    }

    internal static class RiskImportLimits
    {
        // One more than the most rows, plus the header, so "too many rows" can still be told
        public const int MaxSheetRows = Domain.Risks.RiskImport.MaxRows + 2;
    }
}
