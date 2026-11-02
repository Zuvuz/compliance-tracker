using ComplianceTracker.Application.Organizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ComplianceTracker.Web.ViewComponents
{
    // The members of the organisation as a list to choose from, e.g. a risk's owner (US-042 R3).
    // A view component, so a form shown again after an error (DomainExceptionFilter) still has the list.
    public class MemberChoicesViewComponent : ViewComponent
    {
        private readonly OrganizationQueries _organizations;

        public MemberChoicesViewComponent(OrganizationQueries organizations)
        {
            _organizations = organizations;
        }

        public async Task<IViewComponentResult> InvokeAsync(string name, Guid selected)
        {
            var organization = await _organizations.MineAsync(HttpContext.RequestAborted);
            var members = (organization?.Users ?? [])
                .OrderBy(u => u.Name)
                .Select(u => new SelectListItem(u.Name, u.Id.ToString(), u.Id == selected))
                .ToList();
            ViewData["Name"] = name;
            return View(members);
        }
    }
}
