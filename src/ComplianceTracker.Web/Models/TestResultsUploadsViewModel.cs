namespace ComplianceTracker.Web.Models
{
    // US-177: the uploads of test results that can be withdrawn, and whether the member may.
    // #52: every upload, with the names of who uploaded them.
    public sealed record TestResultsUploadsViewModel(
        Domain.Projects.Project Project, IReadOnlyList<Application.Compliance.TestResultsUpload> Uploads, bool CanWithdraw,
        IReadOnlyDictionary<Guid, string> Names)
    {
        public string NameOf(Guid userId) => Names.TryGetValue(userId, out var name) ? name : "Unknown";
    }
}
