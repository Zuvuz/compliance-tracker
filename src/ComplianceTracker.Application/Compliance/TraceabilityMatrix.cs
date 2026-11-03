using System.Globalization;
using System.Text;

namespace ComplianceTracker.Application.Compliance
{
    // US-052: the traceability matrix from each requirement to the user stories that cover it, as CSV.
    // Built from the same data as the Compliance tab (ComplianceQueries), so the numbers always agree.
    public static class TraceabilityMatrix
    {
        // R1, plus the evidence on the requirement (US-057)
        public static readonly IReadOnlyList<string> Columns =
        [
            "Regulation", "Version", "Requirement code", "Requirement title", "Status", "Reason",
            "Story reference", "Story title", "Story state", "Link source", "Linked at", "Linked by", "Evidence"
        ];

        // R2: one line per requirement and linked story; a requirement without a story has one line with the story
        // columns empty. Only active links (R4). nameOf gives the name of a member from their id.
        public static IReadOnlyList<IReadOnlyList<string>> Rows(IEnumerable<FrameworkCompliance> frameworks, Func<Guid, string> nameOf)
        {
            var rows = new List<IReadOnlyList<string>>();
            foreach (var framework in frameworks)
            {
                foreach (var requirement in framework.Requirements)
                {
                    var status = !requirement.IsApplicable ? "Not applicable" : requirement.IsCovered ? "Covered" : "Not covered";
                    string[] start =
                    [
                        framework.Framework.Name,
                        framework.Version.Number.ToString(CultureInfo.InvariantCulture),
                        requirement.Requirement.Code,
                        requirement.Requirement.Title,
                        status,
                        requirement.NotApplicableReason ?? ""
                    ];
                    var evidence = EvidenceText(requirement);

                    if (requirement.Links.Count == 0)
                    {
                        rows.Add([.. start, "", "", "", "", "", "", evidence]);
                        continue;
                    }

                    foreach (var link in requirement.Links)
                    {
                        rows.Add(
                        [
                            .. start,
                            link.WorkItem.ExternalReference,
                            link.WorkItem.Title,
                            link.WorkItem.State.ToString(),
                            link.Link.Source.ToString(),
                            link.Link.LinkedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                            nameOf(link.Link.LinkedById),
                            evidence
                        ]);
                    }
                }
            }

            return rows;
        }

        // R3: UTF-8 with a BOM and ";" as separator, so it opens in Danish Excel. R6: no cell can run as a formula.
        public static byte[] ToCsv(IEnumerable<IReadOnlyList<string>> rows)
        {
            var text = new StringBuilder();
            foreach (var line in rows.Prepend(Columns))
            {
                text.Append(string.Join(';', line.Select(Cell))).Append("\r\n");
            }

            return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
        }

        // e.g. "PR 42 (https://github.com/acme/portal/pull/42) | Pentest report (pentest.pdf, SHA-256 9c1e…, expired)"
        private static string EvidenceText(RequirementCoverage requirement)
            => string.Join(" | ", requirement.Evidence.Select(item =>
            {
                var e = item.Evidence;
                var source = e.IsFile ? $"{e.FileName}, SHA-256 {e.Sha256}" : e.Url;
                return $"{e.Title} ({source}{(item.IsExpired ? ", expired" : "")})";
            }));

        // A cell that starts like a formula gets a leading ' (OWASP CSV injection); cells with ; " or a new line are quoted
        private static string Cell(string value)
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
