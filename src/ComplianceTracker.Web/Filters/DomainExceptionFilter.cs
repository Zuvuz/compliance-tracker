using ComplianceTracker.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace ComplianceTracker.Web.Filters
{
    // Marks a form's view model. When a broken rule stops such a form,
    // the form is shown again with what the user typed (US-003 R3).
    public interface IFormModel
    {
    }

    // Where to go when a broken rule stops an action on an existing page, e.g. "Add framework" on the project page.
    // IdFrom names the action's parameter that holds the id of the page to go back to.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OnDomainErrorAttribute : Attribute
    {
        public OnDomainErrorAttribute(string action)
        {
            Action = action;
        }

        public string Action { get; }
        public string? IdFrom { get; init; }
    }

    // The one place that turns a broken domain rule (DomainException) into an error the user can see (US-029 R4).
    // Two cases:
    // - The action received a form (IFormModel): show the form again with the error and the typed values.
    // - The action has [OnDomainError]: go back to that page (Post/Redirect/Get). The error travels in TempData
    //   and DomainErrorFromTempDataFilter puts it into ModelState there, so the page's validation summary shows it.
    // Any other DomainException is not handled here and becomes a normal error.
    public class DomainExceptionFilter : IAsyncActionFilter
    {
        public const string TempDataKey = "DomainError";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();
            if (executed.Exception is not DomainException error || executed.ExceptionHandled
                || context.Controller is not Controller controller)
            {
                return;
            }

            var formModel = context.ActionArguments.Values.OfType<IFormModel>().FirstOrDefault();
            if (formModel is not null)
            {
                controller.ModelState.AddModelError(string.Empty, error.Message);
                executed.Result = controller.View(((ControllerActionDescriptor)context.ActionDescriptor).ActionName, formModel);
                executed.ExceptionHandled = true;
                return;
            }

            var target = (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo.GetCustomAttribute<OnDomainErrorAttribute>();
            if (target is null)
            {
                return;
            }

            controller.TempData[TempDataKey] = error.Message;
            var routeValues = target.IdFrom is not null && context.ActionArguments.TryGetValue(target.IdFrom, out var id)
                ? new { id }
                : null;
            executed.Result = controller.RedirectToAction(target.Action, routeValues);
            executed.ExceptionHandled = true;
        }
    }

    // Shows an error that DomainExceptionFilter carried over a redirect (see above)
    public class DomainErrorFromTempDataFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is Controller controller
                && controller.TempData[DomainExceptionFilter.TempDataKey] is string message)
            {
                controller.ModelState.AddModelError(string.Empty, message);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
