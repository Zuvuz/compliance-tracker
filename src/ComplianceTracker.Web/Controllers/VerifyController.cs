using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-180: "Verify an audit package". Public (R3): the receiver of a package (an auditor, an authority) needs no account.
    // The uploaded file is read in memory and nothing is stored; the global rate limit per IP address applies (R5).
    [AllowAnonymous]
    public class VerifyController : Controller
    {
        private readonly IPackageSigner _signer;

        public VerifyController(IPackageSigner signer)
        {
            _signer = signer;
        }

        // GET /Verify
        [HttpGet]
        public IActionResult Index() => View(new VerifyViewModel(null, null));

        // POST /Verify/Check: R3, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(PackageManifest.MaxSizeInBytes + 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = PackageManifest.MaxSizeInBytes + 1024 * 1024)]
        public async Task<IActionResult> Check(IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    throw new DomainException("Choose the audit package (a .zip file) to check");
                }

                if (file.Length > PackageManifest.MaxSizeInBytes)
                {
                    throw new DomainException($"The file is larger than {PackageManifest.MaxSizeInBytes / 1024 / 1024} MB");
                }

                using var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken);
                return View(nameof(Index), new VerifyViewModel(PackageManifest.Check(memory.ToArray(), _signer), null));
            }
            catch (DomainException error)
            {
                return View(nameof(Index), new VerifyViewModel(null, error.Message));
            }
        }
    }

    public sealed record VerifyViewModel(PackageCheck? Check, string? Error);
}
