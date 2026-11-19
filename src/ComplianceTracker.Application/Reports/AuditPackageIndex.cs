using System.Net;
using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;

namespace ComplianceTracker.Application.Reports
{
    // US-179: one piece of evidence of a requirement as the package shows it: its file in the package (stored once), or its link
    // US-204 R7: Kind is "Proven by" or "Documented by"
    public record IndexEvidence(string Title, string? Path, string? Url, string? Sha256, string Source, bool Expired, string Kind = "Proven by");

    // US-179: one requirement of the package's index
    public record IndexRow(string Framework, string Code, string Title, string Text, string Status, string Folder,
        IReadOnlyList<string> Stories, IReadOnlyList<IndexEvidence> Evidence, int FailingTests, string? NotApplicableReason);

    // US-179: the audit package sorted per framework and requirement (US-197: a dossier per framework), and an index
    // as HTML (offline, no scripts) and Excel. Files are stored once under evidence/ and referred to from each requirement.
    // Pure, so the edge cases are tested on their own.
    public static class AuditPackageIndex
    {
        public const string HtmlPath = "index.html";
        public const string XlsxPath = "index.xlsx";
        public const int MaxFolderPart = 60;

        // R1: "CRA" + "I.2(d)" → "CRA/I.2(d)"; characters Windows, macOS or Linux do not allow become "-"
        public static string Folder(string framework, string code) => $"{Part(framework)}/{Part(code)}";

        public static string Part(string text)
        {
            var cleaned = Clean(text ?? "").Trim().Trim('.', ' ');
            while (cleaned.Contains("--", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
            }

            cleaned = cleaned.Length <= MaxFolderPart ? cleaned : cleaned[..MaxFolderPart].TrimEnd('.', ' ');
            return cleaned.Length == 0 ? "requirement" : cleaned;
        }

        // Characters Windows, macOS or Linux do not allow in a name become "-"
        private static string Clean(string text)
            => new(text.Select(c => c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(c) ? '-' : c).ToArray());

        public static string Status(RequirementCoverage row)
            => !row.IsApplicable ? "Not applicable"
                : row.IsCoveredNeedingReview ? "Covered, needs review"
                // US-204 R9: proof is shown next to coverage; documentation alone does not prove it
                : row.IsCovered ? (row.IsDocumentedOnly ? "Covered, " + RequirementCoverage.DocumentedProofMissing.ToLowerInvariant() : "Covered")
                : row.EvidenceCovers && row.IsDocumentedOnly ? RequirementCoverage.DocumentedProofMissing
                : "Not covered";

        // R1–R3: the rows of the chosen frameworks; a file's place in the package comes from "files" (evidence id → path, SHA-256)
        public static IReadOnlyList<IndexRow> Rows(IEnumerable<FrameworkCompliance> frameworks,
            IReadOnlyDictionary<Guid, (string Path, string Sha256)> files)
        {
            var rows = new List<IndexRow>();
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var framework in frameworks)
            {
                var name = FrameworkSlugs.Short(framework.Framework.Name);
                foreach (var row in framework.Requirements)
                {
                    // Two codes that become the same folder (e.g. "A/1" and "A:1") get a number
                    var folder = Folder(name, row.Requirement.Code);
                    for (var n = 2; !folders.Add(folder); n++)
                    {
                        folder = $"{Folder(name, row.Requirement.Code)}-{n}";
                    }

                    rows.Add(new IndexRow(name, row.Requirement.Code, row.Requirement.Title, row.Requirement.Description, Status(row), folder,
                        row.Links.Select(l => $"{l.WorkItem.Title} ({RequirementRows.StoryState(l.WorkItem.State)})").ToList(),
                        row.Evidence.Select(e => files.TryGetValue(e.Evidence.Id, out var file)
                                ? new IndexEvidence(e.Evidence.Title, file.Path, null, file.Sha256, Source(e), e.IsExpired, Domain.Mappings.EvidenceKinds.ListName(e.Evidence.Kind))
                                : new IndexEvidence(e.Evidence.Title, null, e.Evidence.Url, null, Source(e), e.IsExpired, Domain.Mappings.EvidenceKinds.ListName(e.Evidence.Kind)))
                            .ToList(),
                        row.FailingTests, row.NotApplicableReason));
                }
            }

            return rows;
        }

        // R3: an evidence file's path, stored once; two files that would get the same path get a number
        public static string EvidencePath(string safeTitle, string fileName, ISet<string> used)
        {
            var name = Clean(fileName);
            var path = $"evidence/{safeTitle}-{name}";
            for (var n = 2; !used.Add(path); n++)
            {
                path = $"evidence/{n}-{safeTitle}-{name}";
            }

            return path;
        }

        // R2: where a piece of evidence comes from, e.g. "Added 01-03-2026, accepted from a test run, valid until 01-03-2027"
        public static string Source(EvidenceItem item)
        {
            var evidence = item.Evidence;
            var text = $"Added {evidence.AddedAt:dd-MM-yyyy}";
            text += evidence.IsFromOrganisation ? ", from the organisation's documents" : "";
            text += item.AcceptedFrom switch
            {
                Domain.Mappings.EvidenceSourceKind.TestRun => ", accepted from a test run",
                Domain.Mappings.EvidenceSourceKind.PullRequest => ", accepted from a pull request",
                _ => ""
            };
            return text + (evidence.ValidUntil is { } until ? $", valid until {until:dd-MM-yyyy}" : "");
        }

        // US-197: the README of a requirement's folder (US-179 R2) is now the framework's dossier (RequirementDossier)

        // R4: one page that works offline: no scripts, no files from the internet; links are relative to the package
        public static string Html(string title, IReadOnlyList<IndexRow> rows)
        {
            static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
            static string Href(string path) => string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>").Append(E(title)).Append("</title><style>")
                .Append("body{font-family:system-ui,sans-serif;margin:2rem;color:#1d2433}table{border-collapse:collapse;width:100%}")
                .Append("th,td{border-bottom:1px solid #d6dbe4;padding:.4rem;text-align:left;vertical-align:top;font-size:.9rem}")
                .Append(".status{white-space:nowrap}h2{margin-top:2rem}")
                .Append("</style></head><body>");
            html.Append("<h1>").Append(E(title)).Append("</h1>");
            html.Append("<p>Every requirement with its status and its evidence. A file is stored once; click it to open it. Each framework's dossier (a PDF) has every requirement with more.</p>");
            foreach (var framework in rows.GroupBy(r => r.Framework))
            {
                html.Append("<h2>").Append(E(framework.Key)).Append("</h2><table><thead><tr><th>Code</th><th>Requirement</th><th>Status</th><th>Evidence</th></tr></thead><tbody>");
                foreach (var row in framework)
                {
                    html.Append("<tr><td><a href='").Append(Href(RequirementDossier.FileName(row.Framework))).Append("'>").Append(E(row.Code)).Append("</a></td>")
                        .Append("<td>").Append(E(row.Title)).Append("</td>")
                        .Append("<td class=\"status\">").Append(E(row.Status)).Append("</td><td>");
                    // US-204 R7: "Proven by" first, then "Documented by"
                    foreach (var list in row.Evidence.GroupBy(e => e.Kind).OrderByDescending(g => g.Key == "Proven by"))
                    {
                        html.Append("<strong>").Append(E(list.Key)).Append(":</strong><br>");
                        foreach (var evidence in list)
                        {
                            html.Append(evidence.Path is not null
                                    ? $"<a href='{Href(evidence.Path)}'>{E(evidence.Title)}</a>"
                                    : $"{E(evidence.Title)} (link: {E(evidence.Url)})")
                                .Append(evidence.Expired ? " (expired)" : "").Append("<br>");
                        }
                    }

                    html.Append("</td></tr>");
                }

                html.Append("</tbody></table>");
            }

            return html.Append("</body></html>").ToString();
        }

        // R4: the same as a workbook: one row per piece of evidence (or per requirement without any)
        public static ReportDocument Sheet(string title, IReadOnlyList<IndexRow> rows)
        {
            var lines = new List<ReportRow>();
            foreach (var row in rows)
            {
                var status = row.Status switch { "Covered" => ReportStatus.Good, "Not covered" => ReportStatus.Bad, "Not applicable" => ReportStatus.Muted, _ => ReportStatus.Warning };
                if (row.Evidence.Count == 0)
                {
                    lines.Add(new ReportRow([row.Framework, row.Code, row.Title, row.Status, "", "", ""], status));
                }

                foreach (var evidence in row.Evidence)
                {
                    lines.Add(new ReportRow([row.Framework, row.Code, row.Title, row.Status, evidence.Title,
                        evidence.Path ?? evidence.Url ?? "", evidence.Sha256 ?? ""], status));
                }
            }

            return new ReportDocument(title, [], [new ReportSheet("Index", ["Framework", "Code", "Requirement", "Status", "Evidence", "File or link", "SHA-256"], 3, lines)]);
        }
    }
}
