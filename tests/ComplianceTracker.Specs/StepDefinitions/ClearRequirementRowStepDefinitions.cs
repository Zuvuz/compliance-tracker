using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-144: the requirement row's groups and sources, its own address, the All sub-tab, a risk as the reason for "not
    // applicable" and unused frameworks. UI contract: Views/Projects/_RequirementRow.cshtml (.row-group[data-group],
    // .row-group-title, .item-source, .story-state, .row-summary, .row-address, .not-applicable-risk) and
    // Views/Projects/_ComplianceTab.cshtml (#compliance-tabs, tr.framework-group, #unused-frameworks).
    [Binding]
    public class ClearRequirementRowStepDefinitions
    {
        private readonly WebScenario _web;

        public ClearRequirementRowStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- When

        [When("I open the address of {string} in {string}")]
        public async Task WhenIOpenTheAddressOf(string code, string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance/cra/{Uri.EscapeDataString(code)}");

        [When("I mark {string} as not applicable because {string} with the risk {string}")]
        public async Task WhenIMarkNotApplicableWithTheRisk(string code, string reason, string risk)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync("Acme Portal")}/compliance/cra");
            var form = Row(code).QuerySelector("form.mark-not-applicable") ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be marked.");
            var option = form.QuerySelectorAll("select[name=riskId] option").FirstOrDefault(o => o.TextContent.Trim().StartsWith(risk + " (", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The risk '{risk}' cannot be chosen.");
            var fields = Hidden(form);
            fields["reason"] = reason;
            fields["riskId"] = option.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I add {string} to the project from the Compliance tab")]
        public async Task WhenIAddToTheProject(string framework)
        {
            var form = _web.Page!.QuerySelectorAll("#unused-frameworks .unused-framework")
                .FirstOrDefault(s => s.GetAttribute("data-framework")!.StartsWith(framework, StringComparison.Ordinal))
                ?.QuerySelector("form.add-unused-framework")
                ?? throw new Xunit.Sdk.XunitException($"'{framework}' is not offered below the tabs.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // ---------------------------------------------------------- Then

        [Then("the row {string} has the groups {string}, {string} and {string}")]
        public void ThenTheRowHasTheGroups(string code, string first, string second, string third)
            => Assert.Equal([first, second, third],
                Row(code).QuerySelectorAll(".row-group .row-group-title").Select(t => Normalize(t.TextContent)).ToList());

        [Then("in the row {string} the story {string} says {string} and {string}")]
        public void ThenTheStorySays(string code, string story, string state, string source)
        {
            var item = Group(code, "stories").QuerySelectorAll(".row-item").Single(i => i.QuerySelector(".linked-story")?.TextContent.Trim() == story);
            Assert.Equal(state, item.QuerySelector(".story-state")?.TextContent.Trim());
            Assert.StartsWith(source, Normalize(item.QuerySelector(".item-source")!.TextContent));
        }

        [Then("in the row {string} the evidence {string} says {string}")]
        public void ThenTheEvidenceSays(string code, string title, string source)
        {
            var item = Group(code, "evidence").QuerySelectorAll(".evidence").Single(i => i.GetAttribute("data-title") == title);
            Assert.StartsWith(source, Normalize(item.QuerySelector(".item-source")!.TextContent));
        }

        [Then("in the row {string} the waiting item can be accepted or rejected")]
        public void ThenTheWaitingItemCanBeDecided(string code)
        {
            var item = Group(code, "waiting").QuerySelector(".row-item")!;
            Assert.NotNull(item.QuerySelector("form.accept-evidence-suggestion"));
            Assert.NotNull(item.QuerySelector("form.reject-evidence-suggestion-row"));
        }

        [Then("the row {string} sums up {string}")]
        public void ThenTheRowSumsUp(string code, string summary) => Assert.Equal(summary, Normalize(Row(code).QuerySelector(".row-summary")!.TextContent));

        [Then("the {string} sub-tab is shown with the row {string} open")]
        public void ThenTheSubTabIsShownWithTheRowOpen(string tab, string code)
        {
            Assert.Equal(tab, _web.Page!.QuerySelector("#compliance-tabs a.active")?.TextContent.Trim());
            Assert.Equal("true", Row(code).GetAttribute("data-open"));
            Assert.Equal("true", Row(code).GetAttribute("data-attention")); // never folded by a remembered choice
        }

        [Then("the row {string} links to its own address")]
        public void ThenTheRowLinksToItsOwnAddress(string code)
            => Assert.EndsWith("/compliance/cra/" + code, Uri.UnescapeDataString(Row(code).QuerySelector("a.row-address")!.GetAttribute("href")!));

        [Then("the requirements are grouped under {string} and {string}")]
        public void ThenTheRequirementsAreGrouped(string first, string second)
        {
            var groups = _web.Page!.QuerySelectorAll("#requirements-table tr.framework-group th").Select(th => th.ChildNodes.OfType<IText>().First().Text.Trim()).ToList();
            Assert.Equal([first, second], groups);
        }

        [Then("the coverage adds up the frameworks")]
        public void ThenTheCoverageAddsUp()
        {
            var total = int.Parse(_web.Page!.QuerySelector("#summary-total")!.TextContent);
            var rows = _web.Page!.QuerySelectorAll("#requirements-table tr.requirement-row").Count(r => !r.ClassList.Contains("text-muted") && r.QuerySelector(".recommended") is null);
            Assert.Equal(rows, total);
            Assert.StartsWith($"0 of {total} requirements covered", _web.Page!.QuerySelector("#coverage")!.TextContent.Trim());
        }

        [Then("the row {string} is not applicable because of the risk {string}")]
        public async Task ThenTheRowNamesTheRisk(string code, string risk)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync("Acme Portal")}/compliance/cra?show=not-applicable");
            Assert.Contains(risk, Row(code).QuerySelector(".not-applicable-risk")?.TextContent ?? "");
        }

        [Then("the Compliance tab of {string} has the sub-tab {string}")]
        public async Task ThenTheComplianceTabHasTheSubTab(string project, string tab)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance");
            Assert.Contains(tab, _web.Page!.QuerySelectorAll("#compliance-tabs a").Select(a => a.TextContent.Trim()));
        }

        // ---------------------------------------------------------- Helpers

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The Compliance tab has no row '{code}'.");

        private IElement Group(string code, string group)
            => Row(code).QuerySelector($".row-group[data-group='{group}']") ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no group '{group}'.");

        private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
