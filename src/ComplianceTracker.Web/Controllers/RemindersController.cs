using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Domain.Reminders;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-183 R5: each person chooses which reminders they get; the organisation admin sets how many days before
    public class RemindersController : Controller
    {
        private readonly ReminderSettingsService _settings;

        public RemindersController(ReminderSettingsService settings)
        {
            _settings = settings;
        }

        // GET /Reminders
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var (user, days, canSetDays) = await _settings.MineAsync(cancellationToken);
            return View(new RemindersViewModel(Enum.GetValues<ReminderKind>().Select(k => (k, !user.HasTurnedOff(k) || !ReminderKinds.CanTurnOff(k))).ToList(),
                days, canSetDays, _settings.MailIsConfigured));
        }

        // POST /Reminders
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Index(List<ReminderKind>? on, int? daysBefore, CancellationToken cancellationToken)
        {
            await _settings.SaveAsync(on ?? [], daysBefore, cancellationToken);
            TempData["RemindersSaved"] = "Your reminders are saved";
            return RedirectToAction(nameof(Index));
        }
    }

    public sealed record RemindersViewModel(IReadOnlyList<(ReminderKind Kind, bool On)> Kinds, int DaysBefore, bool CanSetDays, bool MailIsConfigured);
}
