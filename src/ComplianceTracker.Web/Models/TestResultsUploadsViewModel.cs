namespace ComplianceTracker.Web.Models
{
    // US-177: the uploads of test results that can be withdrawn, and whether the member may
    public sealed record TestResultsUploadsViewModel(Domain.Projects.Project Project, IReadOnlyList<Application.Compliance.TestResultsUpload> Uploads, bool CanWithdraw);
}
