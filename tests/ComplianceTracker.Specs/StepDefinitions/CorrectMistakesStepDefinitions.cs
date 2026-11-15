using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-176: taking back what was made by mistake. UI contract: Views/Risks/Details.cshtml (#withdraw-risk, #reopen-risk,
    // li.risk-action[data-title] with form.remove-risk-action), _ScansTab.cshtml (#closed-findings tr.finding[data-rule]
    // with form.undo-accept-finding; #open-findings), _SecurityEventsTab.cshtml (.security-event[data-title] with
    // form.recorded-in-error).
    [Binding]
    public class CorrectMistakesStepDefinitions
    {
        private readonly WebScenario _web;

        public CorrectMistakesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I withdraw the risk {string} because {string}")]
        public Task WhenIWithdraw(string risk, string reason) => PostOnRiskAsync(risk, "#withdraw-risk", reason);

        [When("I reopen the risk {string} because {string}")]
        public Task WhenIReopen(string risk, string reason) => PostOnRiskAsync(risk, "#reopen-risk", reason);

        [When("I remove the action {string} from the risk {string}")]
        public async Task WhenIRemoveTheAction(string action, string risk)
        {
            await OpenRiskAsync(risk);
            var form = _web.Page!.QuerySelectorAll("li.risk-action").Single(a => a.GetAttribute("data-title") == action).QuerySelector("form.remove-risk-action")
                ?? throw new Xunit.Sdk.XunitException($"The action '{action}' cannot be removed.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I undo the acceptance of the finding {string}")]
        public async Task WhenIUndoTheAcceptance(string rule)
        {
            await OpenTabAsync("scans");
            var form = _web.Page!.QuerySelectorAll("#closed-findings tr.finding").Single(f => f.GetAttribute("data-rule") == rule).QuerySelector("form.undo-accept-finding")
                ?? throw new Xunit.Sdk.XunitException($"The finding '{rule}' offers no undo.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I close {string} as recorded in error because {string}")]
        public async Task WhenIRecordInError(string title, string reason)
        {
            var form = await RecordedInErrorFormAsync(title) ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be closed as recorded in error.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["reason"] = reason });
        }

        [Then("the open register of {string} does not list {string}")]
        public async Task ThenTheRegisterDoesNotList(string project, string risk)
        {
            await OpenTabAsync("risks");
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#risks-table tr.risk-row"), r => r.GetAttribute("data-title") == risk);
        }

        [Then("the risk {string} is open again")]
        public async Task ThenTheRiskIsOpenAgain(string risk)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(Domain.Risks.RiskStatus.Open, await db.Risks.Where(r => r.Title == risk).Select(r => r.Status).SingleAsync());
        }

        [Then("the risk {string} has no action {string}")]
        public async Task ThenTheRiskHasNoAction(string risk, string action)
        {
            await OpenRiskAsync(risk);
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("li.risk-action"), a => a.GetAttribute("data-title") == action);
        }

        [Then("the finding {string} is open")]
        public async Task ThenTheFindingIsOpen(string rule)
        {
            await OpenTabAsync("scans");
            Assert.Contains(_web.Page!.QuerySelectorAll("#open-findings tr.finding"), f => f.GetAttribute("data-rule") == rule);
        }

        [Then("{string} cannot be closed as recorded in error")]
        public async Task ThenCannotBeRecordedInError(string title)
            => Assert.Null(await RecordedInErrorFormAsync(title));

        private async Task<IElement?> RecordedInErrorFormAsync(string title)
        {
            await OpenTabAsync("security-events");
            var item = _web.Page!.QuerySelectorAll(".security-event").Single(e => e.GetAttribute("data-title") == title);
            return item.QuerySelector("form.recorded-in-error");
        }

        private async Task PostOnRiskAsync(string risk, string selector, string reason)
        {
            await OpenRiskAsync(risk);
            var form = _web.Page!.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"The risk page has no {selector}.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>(Hidden(form)) { ["reason"] = reason });
        }

        private async Task OpenRiskAsync(string risk)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        private async Task OpenTabAsync(string tab)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == "Acme Portal").Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/{tab}");
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
