using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.Risks
{
    // US-203 R4: one line of a risk's history: when, who, what kind of change and what changed from and to
    public sealed record RiskHistoryEntry(DateTime At, Guid? UserId, string Kind, string What);

    // US-203 R4, R5: a risk's history, read from the activity log, which is append-only, so the history cannot be edited.
    // The log says which risk an entry is about by starting its text with the risk's title ("Session Hijacking: …"). A rename
    // ("Title: Old → New") is followed back, so the entries written under the old title count too.
    public static class RiskHistory
    {
        private static readonly Dictionary<ActivityAction, string> Kinds = new()
        {
            [ActivityAction.RiskAdded] = "Added",
            [ActivityAction.RiskUpdated] = "Changed",
            [ActivityAction.RiskReviewed] = "Reviewed",
            [ActivityAction.RiskResidualSet] = "Residual risk",
            [ActivityAction.RiskWithdrawn] = "Withdrawn",
            [ActivityAction.RiskReopened] = "Reopened",
            [ActivityAction.RiskActionAdded] = "Action added",
            [ActivityAction.RiskActionLinked] = "Action linked",
            [ActivityAction.RiskActionDone] = "Action done",
            [ActivityAction.RiskActionRemoved] = "Action removed",
            [ActivityAction.RiskActionUnlinked] = "Action unlinked",
            [ActivityAction.RiskMeasuresSuggested] = "Measures suggested by the AI",
            [ActivityAction.RiskMeasureSuggestionApproved] = "Measure approved"
        };

        private const string TitleChange = "Title: ";
        private const string Arrow = " → ";

        // Newest first
        public static IReadOnlyList<RiskHistoryEntry> For(string title, IEnumerable<ActivityLogEntry> entries)
        {
            var titles = new List<string> { title };
            var history = new List<RiskHistoryEntry>();
            foreach (var entry in entries.OrderByDescending(e => e.OccurredAt))
            {
                if (!Kinds.TryGetValue(entry.Action, out var kind) || entry.Details is not { } details
                    || titles.FirstOrDefault(t => details == t || details.StartsWith(t + ": ", StringComparison.Ordinal)) is not { } shown)
                {
                    continue;
                }

                var what = details.Length > shown.Length ? details[(shown.Length + 2)..] : "";
                history.Add(new RiskHistoryEntry(entry.OccurredAt, entry.UserId, kind, what));

                // A rename: the entries before it were written under the old title
                foreach (var part in what.Split("; "))
                {
                    var arrow = part.IndexOf(Arrow, StringComparison.Ordinal);
                    if (part.StartsWith(TitleChange, StringComparison.Ordinal) && arrow > TitleChange.Length
                        && part[(arrow + Arrow.Length)..] == shown)
                    {
                        titles.Add(part[TitleChange.Length..arrow]);
                    }
                }
            }

            return history;
        }
    }
}
