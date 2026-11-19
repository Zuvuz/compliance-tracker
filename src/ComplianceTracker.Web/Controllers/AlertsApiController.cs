using ComplianceTracker.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-160: POST /api/projects/{id}/alerts, for the organisation's monitoring (e.g. Prometheus Alertmanager's webhook).
    // R3: no sign-in, no cookie and no antiforgery; only the project's upload token (US-122) as "Authorization: Bearer
    // ctu_…", and only over HTTPS outside Development. It can only send alerts: there is nothing to read here.
    [ApiController]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("api/projects/{id:guid}/alerts")]
    public class AlertsApiController : ControllerBase
    {
        private readonly MonitoringAlertService _alerts;
        private readonly IWebHostEnvironment _environment;

        public AlertsApiController(MonitoringAlertService alerts, IWebHostEnvironment environment)
        {
            _alerts = alerts;
            _environment = environment;
        }

        [HttpPost]
        [RequestSizeLimit(MonitoringAlertParser.MaxBytes + 1024)]
        public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken)
        {
            if (!Request.IsHttps && !_environment.IsDevelopment())
            {
                return Problem(StatusCodes.Status403Forbidden, "Use HTTPS");
            }

            var header = Request.Headers.Authorization.ToString();
            var secret = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;

            // R6: read at most one byte over the limit, so a body that is too large is never kept whole
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0 && memory.Length <= MonitoringAlertParser.MaxBytes)
            {
                memory.Write(buffer, 0, read);
            }

            var result = await _alerts.ReceiveAsync(id, secret, memory.ToArray(), cancellationToken);
            return result.Status switch
            {
                AlertStatus.Accepted => Ok(new { summary = result.Message, suggested = result.Suggested, closed = result.Closed }),
                AlertStatus.Unauthorized => Unauthorized(result.Message),
                AlertStatus.TooLarge => Problem(StatusCodes.Status400BadRequest, result.Message),
                _ => Problem(StatusCodes.Status400BadRequest, result.Message)
            };
        }

        private ObjectResult Problem(int status, string message) => StatusCode(status, new { error = message });

        private IActionResult Unauthorized(string message)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Problem(StatusCodes.Status401Unauthorized, message);
        }
    }
}
