namespace ComplianceTracker.Web.Models;

// Data for the error page (from the MVC template): the request id helps find the error in the logs
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
