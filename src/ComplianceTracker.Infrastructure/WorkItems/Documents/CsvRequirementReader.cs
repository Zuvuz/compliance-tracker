using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using System.Text;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-032: reads a UTF-8 CSV file. The separator is ";" (Danish Excel) or ",", chosen by which the header row has more of.
    // Quoted fields may hold separators, line breaks and doubled quotes (RFC 4180).
    public class CsvRequirementReader : IRequirementDocumentReader
    {
        // The ID column says what the ID is
        public DocumentKind Kind => DocumentKind.RequirementSpecification;

        public bool UsesIdPattern => false;

        public string Extension => ".csv";

        public RequirementReadResult Read(byte[] content, IdPattern idPattern)
        {
            var text = new UTF8Encoding(false).GetString(content).TrimStart('\uFEFF');
            var firstLine = text.Split('\n')[0];
            var separator = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
            return RequirementTable.Read(Rows(text, separator));
        }

        // Each record with the line it starts on, so "Row 4" matches what the user sees in Excel
        private static IEnumerable<(int Number, IReadOnlyList<string> Cells)> Rows(string text, char separator)
        {
            var cells = new List<string>();
            var field = new StringBuilder();
            var line = 1;
            var recordLine = 1;
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        if (c == '\n')
                        {
                            line++;
                        }

                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == separator)
                {
                    cells.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n')
                {
                    cells.Add(field.ToString().TrimEnd('\r'));
                    field.Clear();
                    yield return (recordLine, cells);
                    cells = new List<string>();
                    line++;
                    recordLine = line;
                }
                else
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || cells.Count > 0)
            {
                cells.Add(field.ToString().TrimEnd('\r'));
                yield return (recordLine, cells);
            }
        }
    }
}
