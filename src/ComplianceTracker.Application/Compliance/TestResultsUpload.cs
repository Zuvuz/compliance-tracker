using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // #52: a suggestion from test results that waits for a decision, with the requirement it is for
    public sealed record TestResultsWaiting(Guid SuggestionId, string Code, string Title, string FrameworkSlug, string SuggestionTitle, bool Blocked);

    // US-177: an upload of test results, and the suggestions from it that still wait (or are blocked), so it can be withdrawn.
    // #52: every upload is listed, also one that made no suggestions; Record says what it led to (null for uploads made before
    // uploads were kept).
    public sealed record TestResultsUpload(string Sha256, string FileName, DateTime UploadedAt, int Waiting)
    {
        public TestRunUpload? Record { get; init; }
        public IReadOnlyList<TestResultsWaiting> WaitingItems { get; init; } = [];
        public int Accepted { get; init; }

        // The pending ones, which "Accept all" accepts; blocked ones cannot be accepted (US-058 R2)
        public int Acceptable => WaitingItems.Count(w => !w.Blocked);

        // US-177: only the suggestions; the uploads with something waiting
        public static IReadOnlyList<TestResultsUpload> From(IEnumerable<EvidenceSuggestion> suggestions)
            => suggestions
                .Where(s => s.Source == EvidenceSourceKind.TestRun && s.Status is EvidenceSuggestionStatus.Pending or EvidenceSuggestionStatus.Blocked)
                .GroupBy(s => s.ExternalId)
                .Select(g => new TestResultsUpload(g.Key, g.First().ResultsFileName ?? "", g.Min(s => s.CreatedAt), g.Count()))
                .OrderByDescending(u => u.UploadedAt)
                .ToList();

        // #52: every kept upload, newest first, with what waits from its file; uploads from before uploads were kept are
        // added from their suggestions when something still waits. A file uploaded twice shows what waits under the newest.
        public static IReadOnlyList<TestResultsUpload> From(
            IEnumerable<EvidenceSuggestion> suggestions, IEnumerable<TestRunUpload> uploads,
            IReadOnlyDictionary<Guid, (string Code, string Title, string FrameworkSlug)> requirements)
        {
            var fromRuns = suggestions.Where(s => s.Source == EvidenceSourceKind.TestRun).ToList();
            List<TestResultsWaiting> WaitingFor(string sha256)
                => fromRuns.Where(s => s.ExternalId == sha256 && s.Status is EvidenceSuggestionStatus.Pending or EvidenceSuggestionStatus.Blocked)
                    .Select(s => requirements.TryGetValue(s.RequirementId, out var r)
                        ? new TestResultsWaiting(s.Id, r.Code, r.Title, r.FrameworkSlug, s.Title, s.Status == EvidenceSuggestionStatus.Blocked)
                        : new TestResultsWaiting(s.Id, "?", "", "", s.Title, s.Status == EvidenceSuggestionStatus.Blocked))
                    .OrderBy(w => w.FrameworkSlug, StringComparer.Ordinal).ThenBy(w => w.Code, StringComparer.Ordinal)
                    .ToList();

            var kept = uploads.OrderByDescending(u => u.UploadedAt).ToList();
            var newestOfFile = kept.GroupBy(u => u.Sha256).ToDictionary(g => g.Key, g => g.First().Id);
            var result = kept.Select(u =>
            {
                var newest = newestOfFile[u.Sha256] == u.Id;
                var waiting = newest ? WaitingFor(u.Sha256) : [];
                return new TestResultsUpload(u.Sha256, u.FileName, u.UploadedAt, waiting.Count)
                {
                    Record = u,
                    WaitingItems = waiting,
                    Accepted = newest ? fromRuns.Count(s => s.ExternalId == u.Sha256 && s.Status == EvidenceSuggestionStatus.Accepted) : 0
                };
            }).ToList();

            foreach (var older in From(fromRuns).Where(o => !newestOfFile.ContainsKey(o.Sha256)))
            {
                result.Add(older with { WaitingItems = WaitingFor(older.Sha256) });
            }

            return result.OrderByDescending(u => u.UploadedAt).ToList();
        }
    }
}
