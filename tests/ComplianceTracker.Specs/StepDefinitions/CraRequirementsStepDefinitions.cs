using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-043: the CRA's requirements and "Not applicable", through the web app with test sign-in.
    // UI contract: on the Compliance tab, rows with data-group, .official-text links, .not-applicable-reason,
    // and the forms .mark-not-applicable and .mark-applicable.
    [Binding]
    public class CraRequirementsStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;

        // The Compliance tab's steps are shared (same WebScenario), e.g. to open the sub-tab with a requirement
        public CraRequirementsStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given / When

        [Given("{string} is not applicable for {string}")]
        public Task GivenIsNotApplicable(string code, string projectName)
            => WhenIMarkNotApplicable(code, projectName, "Not relevant for this product");

        // The row's "Not applicable?" form
        [Given("I mark {string} as not applicable for {string} with the reason {string}")]
        [When("I mark {string} as not applicable for {string} with the reason {string}")]
        public async Task WhenIMarkNotApplicable(string code, string projectName, string reason)
        {
            await _compliance.OpenSubTabWithAsync(projectName, code);
            var form = Row(code).QuerySelector("form.mark-not-applicable")
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be marked not applicable.");

            var fields = HiddenFields(form);
            fields["reason"] = reason;
            await _web.PostFormAsync("/Projects/MarkNotApplicable", fields);
        }

        [When("I make {string} applicable again for {string}")]
        public async Task WhenIMakeApplicableAgain(string code, string projectName)
        {
            await _compliance.OpenSubTabWithAsync(projectName, code);
            var form = Row(code).QuerySelector("form.mark-applicable")
                ?? throw new Xunit.Sdk.XunitException($"'{code}' is not marked not applicable.");
            await _web.PostFormAsync("/Projects/MarkApplicable", HiddenFields(form));
        }

        // R4: the page offers no form for a built-in framework, so the request is sent directly
        [When("I try to add the requirement {string} to the built-in {string}")]
        public async Task WhenITryToAddARequirementToTheBuiltIn(string requirement, string frameworkName)
        {
            await using var db = _web.CreateDbContext();
            var versionId = await (
                    from v in db.FrameworkVersions
                    join f in db.Frameworks on v.FrameworkId equals f.Id
                    where f.Name == frameworkName
                    select v.Id)
                .FirstAsync();

            await _web.OpenAsync("/Frameworks");
            var space = requirement.IndexOf(' ');
            await FrameworkUi.AddRequirementAsync(_web, versionId.ToString(), requirement[..space], requirement[(space + 1)..]);
        }

        // ---------------------------------------------------------- Then

        [Then("I see the group {string} with {int} requirements, starting with {string}")]
        public void ThenISeeTheGroup(string group, int count, string first)
        {
            Assert.Contains(group, _web.Page!.QuerySelectorAll("tr.requirement-group").Select(r => r.TextContent.Trim()));

            var codes = _web.Page!.QuerySelectorAll("tr.requirement-row")
                .Where(r => r.GetAttribute("data-group") == group)
                .Select(r => r.GetAttribute("data-code"))
                .ToList();
            Assert.Equal(count, codes.Count);
            Assert.Equal(first, codes[0]);
        }

        [Then("each requirement links to the official text")]
        public void ThenEachRequirementLinksToTheOfficialText()
        {
            RequirementRowLookup.Overview(_web); // US-153: the rows are on the overview
            var rows = _web.Page!.QuerySelectorAll("tr.requirement-row").ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.StartsWith(
                "https://eur-lex.europa.eu/",
                row.QuerySelector("a.official-text")?.GetAttribute("href") ?? ""));
        }

        [Then("the row {string} is {string} with the reason {string}")]
        public void ThenTheRowIsWithTheReason(string code, string status, string reason)
        {
            var row = Row(code);
            Assert.Equal(status, row.QuerySelector(".requirement-status")?.TextContent.Trim());
            Assert.Equal(reason, row.QuerySelector(".not-applicable-reason")?.TextContent.Trim());
        }

        // ---------------------------------------------------------- Helpers

        private IElement Row(string code)
            => RequirementRowLookup.Find(_web, code)
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
