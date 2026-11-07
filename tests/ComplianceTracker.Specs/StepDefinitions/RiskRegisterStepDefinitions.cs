using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-042: the risk register, through the web app with test sign-in.
    // UI contract: Views/Projects/_RisksTab.cshtml (#risk-matrix cells data-likelihood / data-impact, #risks-table rows
    // risk-row with data-title, .risk-level, .risk-status, .risk-flags, .risk-link, #risk-filter) and Views/Risks
    // (#risk-form, #risk-status, #risk-accepted, #risk-measures, #add-measure, #accept-risk, #close-risk, #review-risk).
    [Binding]
    public class RiskRegisterStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskRegisterStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I add the risk {string} to {string} with likelihood {int} and impact {int}")]
        public async Task WhenIAddTheRisk(string title, string projectName, int likelihood, int impact)
        {
            await _web.OpenAsync($"/Risks/Add?projectId={await ProjectIdAsync(projectName)}");
            var fields = FormFields("#risk-form");
            fields["Title"] = title;
            fields["Likelihood"] = likelihood.ToString();
            fields["Impact"] = impact.ToString();
            await _web.PostFormAsync("/Risks/Add", fields);
        }

        [Given("the risk {string} exists in {string}")]
        public Task GivenTheRiskExists(string title, string projectName)
            => WhenIAddTheRisk(title, projectName, 3, 4);

        [Given("I accept the risk {string} with the reason {string}")]
        [When("I accept the risk {string} with the reason {string}")]
        public async Task WhenIAcceptTheRisk(string title, string reason)
        {
            await PostOnRiskPageAsync(title, "#accept-risk", "/Risks/Accept", reason);
            // US-135: the action waits for a second member, who approves it here; the four-eyes rules are tested in FourEyes.feature
            await FourEyes.ApproveAllAsync(_web);
        }

        [Given("I close the risk {string} with the reason {string}")]
        [When("I close the risk {string} with the reason {string}")]
        public Task WhenICloseTheRisk(string title, string reason)
            => PostOnRiskPageAsync(title, "#close-risk", "/Risks/Close", reason);

        [Given("I mark the risk {string} as reviewed")]
        public Task GivenIMarkTheRiskAsReviewed(string title)
            => PostOnRiskPageAsync(title, "#review-risk", "/Risks/Review", reason: null);

        // R5: chooses the story in the risk page's "Link measure" list
        [When("I link the risk {string} to {string}")]
        public async Task WhenILinkTheRisk(string title, string measure)
        {
            await OpenRiskAsync(title);
            var form = _web.Page!.QuerySelector("#add-measure")!;
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim() == measure)
                ?? throw new Xunit.Sdk.XunitException($"'{measure}' cannot be linked to the risk.");
            var fields = HiddenFields(form);
            fields["measure"] = option.GetAttribute("value")!;
            await _web.PostFormAsync("/Risks/AddMeasure", fields);
        }

        [When("I change the likelihood of {string} to {int}")]
        public async Task WhenIChangeTheLikelihood(string title, int likelihood)
        {
            await OpenRiskAsync(title);
            await _web.OpenAsync(_web.Page!.QuerySelector("#edit-risk-link")!.GetAttribute("href")!);
            var fields = FormFields("#risk-form");
            fields["Likelihood"] = likelihood.ToString();
            await _web.PostFormAsync("/Risks/Edit", fields);
        }

        // ---------------------------------------------------------- Then

        [Then("the risk {string} has the level {string}")]
        public async Task ThenTheRiskHasTheLevel(string title, string level)
        {
            await OpenRisksTabAsync();
            Assert.Equal(level, Row(title).QuerySelector(".risk-level")?.TextContent.Trim());
        }

        [Then("the matrix shows {int} open risk at likelihood {int} and impact {int}")]
        public void ThenTheMatrixShows(int count, int likelihood, int impact)
            => Assert.Equal(count.ToString(),
                _web.Page!.QuerySelector($"#risk-matrix td[data-likelihood='{likelihood}'][data-impact='{impact}']")?.TextContent.Trim());

        [Then("the title field is shown again")]
        public void ThenTheTitleFieldIsShownAgain()
            => Assert.NotNull(_web.Page!.QuerySelector("#risk-form input[name='Title']"));

        [Then("the risk {string} has the status {string}")]
        public async Task ThenTheRiskHasTheStatus(string title, string status)
        {
            await OpenRiskAsync(title);
            Assert.Equal(status, _web.Page!.QuerySelector("#risk-status")?.TextContent.Trim());
        }

        [Then("the risk {string} says {string}")]
        public async Task ThenTheRiskSays(string title, string text)
        {
            await OpenRiskAsync(title);
            Assert.EndsWith(text, _web.Page!.QuerySelector("#risk-accepted")?.TextContent.Trim());
        }

        [Then("the risk {string} shows {string} as a measure")]
        public async Task ThenTheRiskShowsAsAMeasure(string title, string measure)
        {
            await OpenRiskAsync(title);
            Assert.Contains(measure, _web.Page!.QuerySelectorAll("#risk-measures li.risk-measure").Select(li => li.QuerySelector("span")!.TextContent.Trim()));
        }

        [Then("the risk {string} is marked {string}")]
        public void ThenTheRiskIsMarked(string title, string flag)
            => Assert.Contains(flag, Row(title).QuerySelectorAll(".risk-flags .badge").Select(b => b.TextContent.Trim()));

        [Then("the register does not list {string} unless closed risks are shown")]
        public async Task ThenTheRegisterHidesClosedRisks(string title)
        {
            await OpenRisksTabAsync();
            Assert.Null(FindRow(title));

            var action = _web.Page!.QuerySelector("#risk-filter")!.GetAttribute("action")!;
            await _web.OpenAsync(action + "?status=Closed");
            Assert.Equal("Closed", Row(title).QuerySelector(".risk-status")?.TextContent.Trim());
        }

        // ---------------------------------------------------------- Helpers

        private async Task PostOnRiskPageAsync(string title, string formSelector, string path, string? reason)
        {
            await OpenRiskAsync(title);
            var form = _web.Page!.QuerySelector(formSelector)
                ?? throw new Xunit.Sdk.XunitException($"The page of '{title}' has no form {formSelector}.");
            var fields = HiddenFields(form);
            if (reason is not null)
            {
                fields["reason"] = reason;
            }

            await _web.PostFormAsync(path, fields);
        }

        // Opens the risk's own page through its link in the register (closed risks too)
        private async Task OpenRiskAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        private async Task OpenRisksTabAsync()
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync("Acme Portal")}/risks");

        private IElement? FindRow(string title)
            => _web.Page!.QuerySelectorAll("#risks-table tr.risk-row").FirstOrDefault(r => r.GetAttribute("data-title") == title);

        private IElement Row(string title)
            => FindRow(title) ?? throw new Xunit.Sdk.XunitException($"The register does not list '{title}'.");

        // Every field of a form as the browser would send it (inputs, selected options, text areas)
        private Dictionary<string, string> FormFields(string selector)
        {
            var form = _web.Page!.QuerySelector(selector)
                ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no form {selector}.");
            var fields = new Dictionary<string, string>();
            foreach (var input in form.QuerySelectorAll("input[name]").OfType<IHtmlInputElement>())
            {
                fields[input.Name!] = input.Value;
            }

            foreach (var select in form.QuerySelectorAll("select[name]").OfType<IHtmlSelectElement>())
            {
                fields[select.Name!] = select.Value ?? "";
            }

            foreach (var area in form.QuerySelectorAll("textarea[name]").OfType<IHtmlTextAreaElement>())
            {
                fields[area.Name!] = area.Value;
            }

            fields.Remove("__RequestVerificationToken");
            return fields;
        }

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
