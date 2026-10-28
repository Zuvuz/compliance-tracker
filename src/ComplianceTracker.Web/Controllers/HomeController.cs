using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ComplianceTracker.Web.Controllers;

// The public pages: front page, privacy and the error page.
// AllowAnonymous overrides the rule in Program.cs that every page requires sign-in.
[AllowAnonymous]
public class HomeController : Controller
{
    // The front page is public (US-004 R1). Signed-in users are sent on to their organisation.
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Mine", "Organizations");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Shown outside Development when something unexpected goes wrong.
    // It shows a request id for troubleshooting, but no technical details.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
