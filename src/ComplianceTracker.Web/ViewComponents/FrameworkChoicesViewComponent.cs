using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.ViewComponents
{
    // The framework checkboxes on the create-project form (US-011 R2).
    // A view component fetches its own data through dependency injection, so the form can be shown again
    // after an error (DomainExceptionFilter) without the controller having to fill in the list.
    public class FrameworkChoicesViewComponent : ViewComponent
    {
        private readonly FrameworkQueries _frameworks;

        public FrameworkChoicesViewComponent(FrameworkQueries frameworks)
        {
            _frameworks = frameworks;
        }

        // US-007: frameworks that only have a draft cannot be used by projects yet, so they are not offered
        public async Task<IViewComponentResult> InvokeAsync(IReadOnlyCollection<Guid> selected)
        {
            var usable = (await _frameworks.ListAsync(HttpContext.RequestAborted))
                .Where(f => f.CurrentVersion is not null)
                .ToList();
            return View(new FrameworkChoicesViewModel(usable, selected));
        }
    }
}
