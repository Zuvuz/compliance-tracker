using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ComplianceTracker.Web.Security
{
    // US-182 R2, R5: an external auditor whose access is limited to one project may only read that project: its pages and
    // downloads (GET on the Projects pages with that project's id), and the audits of it. Everything else is forbidden, the
    // front page and the project list lead to the project, and every page and download opened is logged.
    // Changing anything is refused by the use cases as well (the Auditor role has no permissions).
    public class AuditorScopeFilter : IAsyncActionFilter
    {
        // What a limited auditor may always reach: signing in and out, invitations, switching organisation
        private static readonly HashSet<string> Open = new(StringComparer.OrdinalIgnoreCase) { "Account", "Home", "Invitations" };

        private readonly CurrentMember _member;
        private readonly AuditorAccessService _access;

        public AuditorScopeFilter(CurrentMember member, AuditorAccessService access)
        {
            _member = member;
            _access = access;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true
                || context.ActionDescriptor is not ControllerActionDescriptor action
                || Open.Contains(action.ControllerName)
                || (action.ControllerName == "Organizations" && action.ActionName is "Switch" or "Mine" or "Create")
                || await _member.OnlyProjectAsync(context.HttpContext.RequestAborted) is not { } project)
            {
                await next();
                return;
            }

            var get = HttpMethods.IsGet(context.HttpContext.Request.Method);
            if (get && action.ControllerName == "Projects" && action.ActionName == "Index")
            {
                context.Result = new RedirectToActionResult("Details", "Projects", new { id = project });
                return;
            }

            if (get && action.ControllerName is "Projects" or "Audits" && IsFor(context, project))
            {
                await _access.LogUseAsync(project, $"{action.ActionName}: {context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}",
                    context.HttpContext.RequestAborted);
                await next();
                return;
            }

            context.Result = new ForbidResult();
        }

        // The request is about the granted project: its id in the route or the query, and no other project's
        private static bool IsFor(ActionExecutingContext context, Guid project)
        {
            var ids = new[] { "id", "projectId" }
                .Select(name => context.RouteData.Values.TryGetValue(name, out var value) ? value?.ToString()
                    : context.HttpContext.Request.Query.TryGetValue(name, out var query) ? query.ToString() : null)
                .Where(v => v is not null)
                .ToList();
            return ids.Count > 0 && ids.All(v => Guid.TryParse(v, out var id) && id == project);
        }
    }
}
