using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // #52: small fixes to the pages. UI contract: Views/Projects/_ProjectHeader.cshtml (#project-groups a[data-group]),
    // the top bar's a#nav-organisation, Views/Risks/Details.cshtml (#add-measure, #risk-measures),
    // Views/Documents/Index.cshtml (#add-document, li.organisation-document with fieldset legends and details.document-more),
    // Views/Projects/_RequirementPage.cshtml (#primary-action, #not-applicable-action, #requirement-more, .needs-action-box).
    [Binding]
    public class SmallUiFixesStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;

        public SmallUiFixesStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- When

        [When("I open the risk {string}")]
        public async Task WhenIOpenTheRisk(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
        }

        [When("I open the Documents page")]
        public Task WhenIOpenTheDocumentsPage() => _web.OpenAsync("/Documents");

        [When("I open the requirement {string} from the {string} sub-tab of {string}")]
        public async Task WhenIOpenTheRequirementFrom(string code, string subTab, string project)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, project);
            var link = RequirementRowLookup.ListRow(_web, code)?.QuerySelector("a.requirement-open")
                ?? throw new Xunit.Sdk.XunitException($"The overview has no link to '{code}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        // ---------------------------------------------------------- Then

        [Then("no tab group of the project opens a menu")]
        public void ThenNoTabGroupOpensAMenu()
        {
            var groups = _web.Page!.QuerySelector("#project-groups") ?? throw new Xunit.Sdk.XunitException("No tab groups.");
            Assert.Empty(groups.QuerySelectorAll(".dropdown-menu, .dropdown-toggle, [data-bs-toggle]"));
        }

        [Then("the tab group {string} opens its first tab")]
        public void ThenTheTabGroupOpensItsFirstTab(string group)
        {
            var link = _web.Page!.QuerySelectorAll("#project-groups a[data-group]").FirstOrDefault(a => a.GetAttribute("data-group") == group)
                ?? throw new Xunit.Sdk.XunitException($"No tab group '{group}'.");
            Assert.Contains("compliance", link.GetAttribute("href")!);
        }

        [Then("the top bar marks {string}")]
        public void ThenTheTopBarMarks(string text)
        {
            var link = _web.Page!.QuerySelector("#nav-organisation") ?? throw new Xunit.Sdk.XunitException("No 'Organisation' in the top bar.");
            Assert.Equal(text, link.TextContent.Trim());
            Assert.Contains("active", link.ClassList);
        }

        [Then("{string} comes before {string}")]
        public void ThenComesBefore(string first, string second)
        {
            var a = _web.Page!.QuerySelector(first) ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no {first}.");
            var b = _web.Page.QuerySelector(second) ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no {second}.");
            Assert.True(a.CompareDocumentPosition(b).HasFlag(DocumentPositions.Following), $"{first} is not before {second}.");
        }

        [Then("the document {string} has the groups {string} and {string}")]
        public void ThenTheDocumentHasTheGroups(string title, string first, string second)
        {
            var legends = Document(title).QuerySelectorAll("fieldset legend").Select(l => l.TextContent.Trim()).ToList();
            Assert.Equal([first, second], legends);
        }

        [Then("{string} of the document {string} is under {string}")]
        public void ThenIsUnderTheDocumentMenu(string button, string title, string menu)
        {
            var more = Document(title).QuerySelector("details.document-more") ?? throw new Xunit.Sdk.XunitException("No menu on the document.");
            Assert.Equal(menu, more.QuerySelector("summary")!.TextContent.Trim());
            Assert.Contains(button, more.QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
        }

        [Then("the table lists {string} among others")]
        public void ThenTheTableListsAmongOthers(string code)
            => Assert.Contains(code, _web.Page!.QuerySelectorAll("#requirements-table tr.requirement-row").Select(r => r.GetAttribute("data-code")));

        [Then("the page has the primary action {string} and next to it {string}")]
        public void ThenThePageHasThePrimaryActionAndNextToIt(string primary, string next)
        {
            var page = _web.Page!.QuerySelector("article.requirement-page") ?? throw new Xunit.Sdk.XunitException("Not a requirement page.");
            Assert.Equal(primary, Assert.Single(page.QuerySelectorAll(".btn-primary").Where(b => b.Id == "primary-action")).TextContent.Trim());
            var action = page.QuerySelector("#not-applicable-action") ?? throw new Xunit.Sdk.XunitException($"No '{next}' button.");
            Assert.Equal(next, action.QuerySelector("summary")!.TextContent.Trim());
            Assert.Null(action.Closest("#requirement-more"));
            Assert.NotNull(action.QuerySelector("form.mark-not-applicable"));
        }

        [Then("the action {string} is under {string}")]
        public void ThenTheActionIsUnder(string action, string menu)
        {
            var more = _web.Page!.QuerySelector("#requirement-more") ?? throw new Xunit.Sdk.XunitException($"No '{menu}' menu.");
            Assert.Equal(menu, more.QuerySelector("summary")!.TextContent.Trim());
            var labels = more.QuerySelectorAll("button, summary").Select(b => b.TextContent.Trim()).ToList();
            Assert.Contains(action, labels);
            Assert.DoesNotContain("Not applicable", labels);
        }

        [Then("the needs-action box has {string} and no {string}")]
        public void ThenTheNeedsActionBoxHas(string present, string absent)
        {
            var box = _web.Page!.QuerySelector(".needs-action-box") ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no needs-action box.");
            var labels = box.QuerySelectorAll("button, summary").Select(b => b.TextContent.Trim()).ToList();
            Assert.Contains(present, labels);
            Assert.DoesNotContain(absent, labels);
        }

        // #52: a measure on a risk's page
        [Then("the measure {string} says {string} and offers {string}")]
        public void ThenTheMeasureSays(string measure, string behind, string action)
        {
            var item = Measure(measure);
            Assert.Equal(behind, item.QuerySelector(".measure-behind")?.TextContent.Trim());
            Assert.Equal(action, item.QuerySelector("a.measure-add-evidence")?.TextContent.Trim());
        }

        [Then("the measure {string} says {string} and leads to {string}")]
        public void ThenTheStoryMeasureSays(string measure, string behind, string code)
        {
            var item = Measure(measure);
            Assert.Equal(behind, item.QuerySelector(".measure-behind")?.TextContent.Trim());
            Assert.Contains(code, item.QuerySelectorAll("a.measure-story-requirement").Select(a => a.TextContent.Trim()));
        }

        [When("I follow the measure {string}")]
        public async Task WhenIFollowTheMeasure(string measure)
        {
            var link = Measure(measure).QuerySelector("a.measure-link") ?? throw new Xunit.Sdk.XunitException($"'{measure}' leads nowhere.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the page leads back to the risk {string}")]
        public async Task ThenThePageLeadsBack(string title)
        {
            var back = _web.Page!.QuerySelector("a#back-to-risk") ?? throw new Xunit.Sdk.XunitException("No way back to the risk.");
            await _web.OpenAsync(back.GetAttribute("href")!);
            Assert.Equal(title, _web.Page!.QuerySelector("#risk-title")?.TextContent.Trim());
        }

        private IElement Measure(string measure)
            => _web.Page!.QuerySelectorAll("#risk-measures li.risk-measure").FirstOrDefault(li => (li.GetAttribute("data-measure") ?? "").StartsWith(measure, StringComparison.Ordinal))
               ?? throw new Xunit.Sdk.XunitException($"The risk has no measure '{measure}'.");

        private IElement Document(string title)
            => _web.Page!.QuerySelectorAll("li.organisation-document").FirstOrDefault(d => d.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no document '{title}'.");
    }
}
