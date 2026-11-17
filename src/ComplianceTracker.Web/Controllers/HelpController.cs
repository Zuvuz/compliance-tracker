using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-173: the Help page with the glossary. The "?" after a term links here when JavaScript is off (R3).
    public class HelpController : Controller
    {
        // GET /Help
        [HttpGet]
        public IActionResult Index() => View(Glossary.Terms);
    }
}
