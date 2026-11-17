using ComplianceTracker.Application.Scanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-148: POST /api/projects/{id}/sboms, for build pipelines, with the same upload token as scans (US-122): no sign-in,
    // no cookie and no antiforgery; only "Authorization: Bearer ctu_…", and only over HTTPS outside Development.
    // Form fields: file (CycloneDX or SPDX JSON), version (required), commit (optional).
    [ApiController]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("api/projects/{id:guid}/sboms")]
    public class SbomUploadApiController : ControllerBase
    {
        // A little above 50 MB, so the use case answers 413 with its own message
        private const long MaxRequestSize = 60L * 1024 * 1024;

        private readonly SbomService _sboms;
        private readonly IWebHostEnvironment _environment;

        public SbomUploadApiController(SbomService sboms, IWebHostEnvironment environment)
        {
            _sboms = sboms;
            _environment = environment;
        }

        [HttpPost]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        public async Task<IActionResult> Upload(Guid id, IFormFile? file, [FromForm] string? version, [FromForm] string? commit, CancellationToken cancellationToken)
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

            var result = await _sboms.UploadFromPipelineAsync(id, secret, file?.FileName, file?.Length ?? 0, read, version, commit, cancellationToken);
            return result.Status switch
            {
                PipelineUploadStatus.Created => StatusCode(StatusCodes.Status201Created, new
                {
                    summary = result.Message,
                    sbomId = result.Sbom!.Id,
                    version = result.Sbom.ProductVersion,
                    commit = result.Sbom.CommitSha
                }),
                PipelineUploadStatus.Unauthorized => Unauthorized(result.Message),
                PipelineUploadStatus.TooLarge => Problem(StatusCodes.Status413PayloadTooLarge, result.Message),
                PipelineUploadStatus.UnsupportedFormat => Problem(StatusCodes.Status415UnsupportedMediaType, result.Message),
                PipelineUploadStatus.TooManyRequests => TooMany(result.Message),
                _ => Problem(StatusCodes.Status400BadRequest, result.Message)
            };
        }

        private ObjectResult Problem(int status, string message) => StatusCode(status, new { error = message });

        // R6: the same answer for a wrong, revoked or other project's token
        private IActionResult Unauthorized(string message)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Problem(StatusCodes.Status401Unauthorized, message);
        }

        private IActionResult TooMany(string message)
        {
            Response.Headers.RetryAfter = "3600";
            return Problem(StatusCodes.Status429TooManyRequests, message);
        }
    }
}
