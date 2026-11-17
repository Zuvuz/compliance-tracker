namespace ComplianceTracker.Web.Models
{
    // US-177: the "⋯" menu next to an upload with "Withdraw" and a reason. Action is the controller action; IdName and
    // IdValue the upload's id field; What says what is withdrawn, e.g. "the Trivy scan".
    public sealed record WithdrawMenu(Guid ProjectId, string Action, string IdName, string IdValue, string What, string? Effect = null);
}
