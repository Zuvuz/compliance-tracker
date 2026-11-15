using System.Text;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // US-194 R3: one phase change from the activity log, with what was missing at the time (US-047 R5)
    public sealed record PhaseChange(DateTime At, string By, string Change, string? Missing);

    // US-194: the project's phase, every phase's checklist and the phase changes, for the reports
    public sealed record ProjectLifecycle(LifecyclePhase Phase, IReadOnlyList<ChecklistLine> Lines, IReadOnlyList<PhaseChange> History)
    {
        public IReadOnlyList<ChecklistLine> Current => Lines.Where(l => l.Item.Phase == Phase && l.State != ChecklistState.NotChosen).ToList();

        public int Done => Current.Count(l => l.State == ChecklistState.Done);

        public int Counted => Current.Count(l => l.State is ChecklistState.Done or ChecklistState.Missing);

        public IReadOnlyList<ChecklistLine> Missing => Current.Where(l => l.State == ChecklistState.Missing).ToList();

        // R1, R4: e.g. "Verification · 3 of 7 done"
        public string Summary => Counted == 0 ? Phase.ToString() : $"{Phase} · {Done} of {Counted} done";
    }

    // US-194: the lifecycle as report sheets, a text file and document blocks. Pure, so the edge cases are tested on their own.
    public static class LifecycleReport
    {
        // R3: the phase changes from the log, each with the missing items logged next to it
        public static IReadOnlyList<PhaseChange> History(IEnumerable<ActivityLogEntry> entries, Func<Guid?, string> nameOf)
        {
            var list = entries.OrderBy(e => e.OccurredAt).ToList();
            var missing = list.Where(e => e.Action == ActivityAction.PhaseChangedWithItemsMissing).ToList();
            return list.Where(e => e.Action == ActivityAction.ProjectPhaseChanged)
                .Select(e =>
                {
                    var next = missing.FirstOrDefault(m => m.UserId == e.UserId && m.OccurredAt >= e.OccurredAt && m.OccurredAt - e.OccurredAt < TimeSpan.FromMinutes(1));
                    var details = next?.Details;
                    var at = details?.IndexOf("missing: ", StringComparison.Ordinal) ?? -1;
                    return new PhaseChange(e.OccurredAt, nameOf(e.UserId), (e.Details ?? "").Replace("Phase: ", "", StringComparison.Ordinal),
                        at < 0 ? null : details![(at + "missing: ".Length)..]);
                })
                .ToList();
        }

        public static string State(ChecklistLine line) => line.State switch
        {
            ChecklistState.Done => "Done",
            ChecklistState.TurnedOff => "Turned off",
            ChecklistState.NotChosen => "Not chosen",
            _ => "Missing"
        };

        // R2: who and when, or the reason an item is off
        public static string Note(ChecklistLine line, Func<Guid?, string> nameOf)
            => line.State == ChecklistState.TurnedOff ? $"Reason: {line.Entry?.OffReason}"
                : line.Entry is { Done: true } done ? $"{nameOf(done.DoneById)} {done.DoneAt:dd-MM-yyyy}{(done.Comment is { } c ? $": {c}" : "")}{(done.EvidenceUrl is { } u ? $" ({u})" : "")}"
                : line.State == ChecklistState.Done ? "From the tracker's data"
                : "";

        // R2: every phase's items that count (not the optional tests nobody chose)
        public static ReportSheet Sheet(ProjectLifecycle lifecycle, Func<Guid?, string> nameOf)
            => new("Lifecycle checklist", ["Phase", "Item", "Regulation", "Status", "Who, when or why"], 3,
                lifecycle.Lines.Where(l => l.State != ChecklistState.NotChosen)
                    .OrderBy(l => l.Item.Phase)
                    .Select(l => new ReportRow([l.Item.Phase.ToString(), l.Item.Text, l.Item.Source, State(l), Note(l, nameOf)],
                        l.State switch { ChecklistState.Done => ReportStatus.Good, ChecklistState.Missing => ReportStatus.Bad, _ => ReportStatus.Muted }))
                    .ToList());

        // R3
        public static ReportSheet HistorySheet(ProjectLifecycle lifecycle)
            => new("Phase changes", ["When", "Who", "Change", "Missing at the time"], -1,
                lifecycle.History.Select(h => new ReportRow([h.At.ToString("dd-MM-yyyy HH:mm"), h.By, h.Change, h.Missing ?? ""],
                    h.Missing is null ? ReportStatus.Normal : ReportStatus.Warning)).ToList());

        // R2, R3: lifecycle.txt in the audit package
        public static string Text(string project, ProjectLifecycle lifecycle, Func<Guid?, string> nameOf)
        {
            var text = new StringBuilder();
            text.AppendLine($"Lifecycle of {project}").AppendLine();
            text.AppendLine($"Phase now: {lifecycle.Summary}").AppendLine();
            text.AppendLine("Phase changes");
            if (lifecycle.History.Count == 0)
            {
                text.AppendLine("  None recorded");
            }

            foreach (var change in lifecycle.History)
            {
                text.AppendLine($"  {change.At:dd-MM-yyyy HH:mm}  {change.Change}  ({change.By})");
                if (change.Missing is { } missing)
                {
                    text.AppendLine($"      missing: {missing}");
                }
            }

            foreach (var phase in lifecycle.Lines.Where(l => l.State != ChecklistState.NotChosen).GroupBy(l => l.Item.Phase).OrderBy(g => g.Key))
            {
                text.AppendLine().AppendLine($"Checklist: {phase.Key}");
                foreach (var line in phase)
                {
                    var note = Note(line, nameOf);
                    text.AppendLine($"  [{State(line)}] {line.Item.Text} – {line.Item.Source}{(note.Length == 0 ? "" : $" – {note}")}");
                }
            }

            return text.ToString();
        }
    }
}
