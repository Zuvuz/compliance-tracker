using ComplianceTracker.Application.Scanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-122: POST /api/projects/{id}/scans, for build pipelines. R5: no sign-in, no cookie and no antiforgery; only the
    // project's upload token as "Authorization: Bearer ctu_…", and only over HTTPS outside Development. It can only upload
    // (R1): there is nothing to read here.
    [ApiController]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("api/projects/{id:guid}/scans")]
    public class ScanUploadApiController : ControllerBase
    {
        // A little above 50 MB, so the use case answers 413 with its own message
        private const long MaxRequestSize = 60L * 1024 * 1024;

        private readonly ScanService _scans;
        private readonly IWebHostEnvironment _environment;

        public ScanUploadApiController(ScanService scans, IWebHostEnvironment environment)
        {
            _scans = scans;
            _environment = environment;
        }

        [HttpPost]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        public async Task<IActionResult> Upload(Guid id, IFormFile? file, [FromForm] string? commit, [FromForm] string? version, CancellationToken cancellationToken)
        {
            if (!Request.IsHttps && !_environment.IsDevelopment())
            {
                return Problem(StatusCodes.Status403Forbidden, "Use HTTPS");
            }

            var header = Request.Headers.Authorization.ToString();
            var secret = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;

            Func<CancellationToken, Task<byte[]>>? read = file is null ? null : async token =>
            {
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory, token);
                return memory.ToArray();
            };

            var result = await _scans.UploadFromPipelineAsync(id, secret, file?.FileName, file?.Length ?? 0, read, commit, version, cancellationToken);
            return result.Status switch
            {
                PipelineUploadStatus.Created => StatusCode(StatusCodes.Status201Created, new
                {
                    summary = result.Message,
                    scanId = result.Scan!.Id,
                    commit = result.Scan.CommitSha,
                    version = result.Scan.Version
                }),
                PipelineUploadStatus.Unauthorized => Unauthorized(result.Message),
                PipelineUploadStatus.TooLarge => Problem(StatusCodes.Status413PayloadTooLarge, result.Message),
                PipelineUploadStatus.UnsupportedFormat => Problem(StatusCodes.Status415UnsupportedMediaType, result.Message),
                PipelineUploadStatus.TooManyRequests => TooMany(result.Message),
                _ => Problem(StatusCodes.Status400BadRequest, result.Message)
            };
        }

        private ObjectResult Problem(int status, string message) => StatusCode(status, new { error = message });

        // R3: the same answer for a wrong, revoked or other project's token
        private IActionResult Unauthorized(string message)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Problem(StatusCodes.Status401Unauthorized, message);
        }

        // R6
        private IActionResult TooMany(string message)
        {
            Response.Headers.RetryAfter = "3600";
            return Problem(StatusCodes.Status429TooManyRequests, message);
        }
    }
}
