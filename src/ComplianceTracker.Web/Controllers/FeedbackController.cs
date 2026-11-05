using ComplianceTracker.Application.Feedback;
using ComplianceTracker.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-063: /Feedback, the form behind "Send feedback" in the footer. For every signed-in member.
    public class FeedbackController : Controller
    {
        // R3: 5 MB for the screenshot, plus the form's text
        private const long MaxRequestSize = 6 * 1024 * 1024;

        private readonly FeedbackService _feedback;

        public FeedbackController(FeedbackService feedback)
        {
            _feedback = feedback;
        }

        [HttpGet]
        public IActionResult Index(string? page)
            => View(new FeedbackForm { Page = page });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        public async Task<IActionResult> Index(FeedbackForm form, IFormFile? screenshot, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? content = null;
                if (screenshot is { Length: > 0 })
                {
                    using var buffer = new MemoryStream();
                    await screenshot.CopyToAsync(buffer, cancellationToken);
                    content = buffer.ToArray();
                }

                var sent = await _feedback.SendAsync(form.Page, form.AsA ?? "", form.IWant ?? "", form.SoThat ?? "",
                    form.Rules, form.Examples, screenshot?.FileName, content, cancellationToken);

                // R4: back to the page it came from (the domain keeps only a path inside the tracker)
                TempData["FeedbackSent"] = "Thank you, your feedback has been sent";
                return LocalRedirect(sent.Page);
            }
            catch (DomainException e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return View(form);
            }
        }
    }

    public class FeedbackForm
    {
        public string? Page { get; set; }
        public string? AsA { get; set; }
        public string? IWant { get; set; }
        public string? SoThat { get; set; }
        public string? Rules { get; set; }
        public string? Examples { get; set; }
    }
}
