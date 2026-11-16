using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Reports
{
    // US-197: one PDF per framework in the audit package: every requirement with its status, stories, failing tests and
    // evidence (title, path in the package, SHA-256, source), and the framework's lifecycle checklist items. Also turns the
    // package's own texts into documents, so they can be made as PDF and sealed. Pure, so the edge cases are tested on their own.
    public static class RequirementDossier
    {
        // R1: "CRA" → "CRA-dossier.pdf"
        public static string FileName(string framework) => $"{AuditPackageIndex.Part(framework)}-dossier.pdf";

        // R4: what the dossier and the contents say about the seal
        public static string SealNote(string? sealedBy)
            => sealedBy is null
                ? "Not sealed: the organisation has no usable seal (US-132). Check the files against the SHA-256 values in the tracker."
                : $"Every PDF the package made is sealed by {sealedBy}. A changed PDF shows an invalid seal.";

        public static GeneratedDocument Build(string framework, string project, IReadOnlyList<IndexRow> rows, ProjectLifecycle? lifecycle,
            Func<Guid?, string> nameOf, string? sealedBy, DateTime madeAtUtc)
        {
            var blocks = new List<DocumentBlock>
            {
                new(DocumentBlockKind.Title, $"{framework} dossier – {project}"),
                new(DocumentBlockKind.Paragraph, $"Made {madeAtUtc:dd-MM-yyyy HH:mm} UTC. {SealNote(sealedBy)}"),
                new(DocumentBlockKind.Paragraph, rows.Count == 0
                    ? "The framework has no requirements."
                    : $"{rows.Count} requirement{(rows.Count == 1 ? "" : "s")}: {rows.Count(r => r.Status == "Covered")} covered, "
                      + $"{rows.Count(r => r.Status == "Not covered")} not covered, {rows.Count(r => r.Status == "Not applicable")} not applicable.")
            };

            foreach (var row in rows)
            {
                blocks.Add(new(DocumentBlockKind.Heading2, $"{row.Code} – {row.Title}"));
                if (!string.IsNullOrWhiteSpace(row.Text))
                {
                    blocks.Add(new(DocumentBlockKind.Paragraph, row.Text.Trim()));
                }

                blocks.Add(new(DocumentBlockKind.Paragraph, $"Status: {row.Status}{(row.NotApplicableReason is { } why ? $" ({why})" : "")}"
                    + (row.FailingTests > 0 ? $" · Failing tests: {row.FailingTests}" : "")));
                blocks.Add(new(DocumentBlockKind.Paragraph, row.Stories.Count == 0 ? "User stories: none" : "User stories:"));
                blocks.AddRange(row.Stories.Select(s => new DocumentBlock(DocumentBlockKind.Bullet, s)));
                blocks.Add(new(DocumentBlockKind.Paragraph, row.Evidence.Count == 0 ? "Evidence: none" : "Evidence:"));
                blocks.AddRange(row.Evidence.Select(e => new DocumentBlock(DocumentBlockKind.Bullet,
                    $"{e.Title}{(e.Expired ? " (expired)" : "")}: "
                    + (e.Path is not null ? $"{e.Path}, SHA-256 {e.Sha256}" : $"link {e.Url}")
                    + $". {e.Source}")));
            }

            // R1: the framework's lifecycle checklist items
            var lines = lifecycle?.Lines.Where(l => l.State != ChecklistState.NotChosen && Belongs(l.Item.Regulation, framework)).OrderBy(l => l.Item.Phase).ToList() ?? [];
            blocks.Add(new(DocumentBlockKind.Heading1, "Lifecycle checklist"));
            if (lines.Count == 0)
            {
                blocks.Add(new(DocumentBlockKind.Paragraph, "No lifecycle checklist items for this framework."));
            }

            blocks.AddRange(lines.Select(l => new DocumentBlock(DocumentBlockKind.Bullet,
                $"{l.Item.Phase}: {l.Item.Text} ({l.Item.Source}) – {LifecycleReport.State(l)}"
                + (LifecycleReport.Note(l, nameOf) is { Length: > 0 } note ? $", {note}" : ""))));

            return new GeneratedDocument(FileName(framework), blocks);
        }

        // A built-in item by its regulation (US-047), or an item the organisation added for this framework (US-192, US-193)
        public static bool Belongs(string regulation, string framework)
            => regulation != BuiltInChecklist.OptionalTest && (BuiltInChecklist.Follows(regulation, framework) || regulation == framework);

        // R2: a text of the package as a document: the first line is the title, "- " lines are bullets, the rest paragraphs
        public static GeneratedDocument FromText(string fileName, string text)
        {
            var blocks = new List<DocumentBlock>();
            foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                blocks.Add(blocks.Count == 0 ? new(DocumentBlockKind.Title, line)
                    : line.StartsWith("- ", StringComparison.Ordinal) ? new(DocumentBlockKind.Bullet, line[2..])
                    : raw.StartsWith(' ') ? new(DocumentBlockKind.Bullet, line)
                    : new(DocumentBlockKind.Heading2, line));
            }

            // A line that is followed by indented lines is a heading; any other line at the left edge is a paragraph
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Kind == DocumentBlockKind.Heading2 && (i + 1 == blocks.Count || blocks[i + 1].Kind != DocumentBlockKind.Bullet))
                {
                    blocks[i] = blocks[i] with { Kind = DocumentBlockKind.Paragraph };
                }
            }

            return new GeneratedDocument(fileName, blocks.Count == 0 ? [new(DocumentBlockKind.Title, fileName)] : blocks);
        }
    }
}
