using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-177: one upload of test results, found by its file's SHA-256: when, the file, and how many of its suggestions
    // still wait or are blocked
    public sealed record TestResultsUpload(string Sha256, string FileName, DateTime UploadedAt, int Waiting)
    {
        public static IReadOnlyList<TestResultsUpload> From(IEnumerable<EvidenceSuggestion> suggestions)
            => suggestions
                .Where(s => s.Source == EvidenceSourceKind.TestRun && s.Status is EvidenceSuggestionStatus.Pending or EvidenceSuggestionStatus.Blocked)
                .GroupBy(s => s.ExternalId)
                .Select(g => new TestResultsUpload(g.Key, g.First().ResultsFileName ?? "", g.Min(s => s.CreatedAt), g.Count()))
                .OrderByDescending(u => u.UploadedAt)
                .ToList();
    }
}
