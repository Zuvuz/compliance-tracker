using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-202: Views/Risks/Details.cshtml (section#what-reduces; li.risk-measure with ul.measure-actions li.risk-action[data-title]
    // and form.add-risk-action-form (measureId); form.move-risk-action (to); #add-measure with optgroup#relevant-measures).
    [Binding]
    public class WhatReducesARiskStepDefinitions
    {
        private readonly WebScenario _web;

        public WhatReducesARiskStepDefinitions(WebScenario web) => _web = web;

        private async Task OpenAsync(string risk)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == risk).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        private IElement Measure(string code)
            => _web.Page!.QuerySelectorAll("#what-reduces li.risk-measure").FirstOrDefault(m => (m.GetAttribute("data-measure") ?? "").StartsWith(code + " ", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The risk has no measure '{code}'.");

        private Task PostAsync(IElement form, Dictionary<string, string> fields)
        {
            foreach (var input in form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken"))
            {
                fields.TryAdd(input.GetAttribute("name")!, input.GetAttribute("value") ?? "");
            }

            return _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Given("I add the action {string} under {string} on {string}")]
        [When("I add the action {string} under {string} on {string}")]
        public async Task WhenIAddTheActionUnder(string action, string code, string risk)
        {
            await OpenAsync(risk);
            var form = Measure(code).QuerySelector("form.add-risk-action-form") ?? throw new Xunit.Sdk.XunitException($"'{code}' has no \"+ Add action\".");
            var owner = form.QuerySelectorAll("select[name=ownerId] option").FirstOrDefault(o => o.HasAttribute("selected"))
                        ?? form.QuerySelector("select[name=ownerId] option")!;
            await PostAsync(form, new Dictionary<string, string> { ["title"] = action, ["ownerId"] = owner.GetAttribute("value")!, ["dueOn"] = "" });
        }

        [When("I move the action {string} of {string} under {string}")]
        public async Task WhenIMoveTheAction(string action, string risk, string code)
        {
            await OpenAsync(risk);
            var item = _web.Page!.QuerySelectorAll("li.risk-action").FirstOrDefault(a => a.GetAttribute("data-title") == action)
                ?? throw new Xunit.Sdk.XunitException($"'{risk}' has no action '{action}'.");
            var form = item.QuerySelector("form.move-risk-action") ?? throw new Xunit.Sdk.XunitException("The action cannot be moved.");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The action cannot be moved under '{code}'.");
            await PostAsync(form, new Dictionary<string, string> { ["to"] = option.GetAttribute("value")! });
        }

        [Then("\"What reduces this risk\" shows {string} with the action {string}")]
        public void ThenTheMeasureHasTheAction(string code, string action)
            => Assert.Contains(Measure(code).QuerySelectorAll("ul.measure-actions li.risk-action"), a => a.GetAttribute("data-title") == action);

        [Then("the page has no section {string} or {string}")]
        public void ThenNoSection(string first, string second)
        {
            var headings = _web.Page!.QuerySelectorAll("h2").Select(h => h.TextContent.Trim()).ToList();
            Assert.DoesNotContain(first, headings);
            Assert.DoesNotContain(second, headings);
        }

        [Then("the most relevant requirement offered as a measure is {string}")]
        public void ThenTheMostRelevantIs(string code)
        {
            var first = _web.Page!.QuerySelector("#add-measure optgroup#relevant-measures option")
                ?? throw new Xunit.Sdk.XunitException("The measure list has no \"Most relevant\" group.");
            Assert.StartsWith(code + " ", first.TextContent.Trim(), StringComparison.Ordinal);
        }
    }
}
