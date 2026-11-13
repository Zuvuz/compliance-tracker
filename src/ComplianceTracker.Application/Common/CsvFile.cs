using System.Text;

namespace ComplianceTracker.Application.Common
{
    // US-120 R3, R6 and US-055 R6: a CSV file that opens in Danish Excel (UTF-8 with a BOM, ";" between cells) and where no
    // cell can run as a formula (OWASP CSV injection)
    public static class CsvFile
    {
        public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
        {
            var text = new StringBuilder();
            foreach (var line in rows.Prepend(header))
            {
                text.Append(string.Join(';', line.Select(Cell))).Append("\r\n");
            }

            return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
        }

        // A cell that starts like a formula gets a leading '; cells with ; " or a new line are quoted
        public static string Cell(string value)
        {
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            {
                value = "'" + value;
            }

            return value.IndexOfAny([';', '"', '\n', '\r']) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
