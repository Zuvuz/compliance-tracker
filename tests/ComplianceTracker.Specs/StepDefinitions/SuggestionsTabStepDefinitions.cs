using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-119: the Suggestions tab, like a user. UI contract: Views/Projects/_SuggestionsTab.cshtml (.gap-category[data-category]
    // with .gap-empty, .gap (data-key, data-title) with .gap-missing, form.dismiss-gap and a.add-risk-from-gap).
    [Binding]
    public class SuggestionsTabStepDefinitions
    {
        private const string Project = "Acme Portal";
        private readonly WebScenario _web;
        private readonly RiskActionsStepDefinitions _actions;

        public SuggestionsTabStepDefinitions(WebScenario web, RiskActionsStepDefinitions actions)
        {
            _web = web;
            _actions = actions;
        }

        [Given("I open the {string} tab of {string} for gaps")]
        [When("I open the {string} tab of {string} for gaps")]
        public async Task WhenIOpenTheTab(string _, string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/suggestions");
        }

        // The title starts with the text, e.g. "I.2(i) " for the requirement I.2(i)
        [Given("I dismiss the gap {string} because {string}")]
        [When("I dismiss the gap {string} because {string}")]
        public async Task WhenIDismissTheGap(string title, string reason)
        {
            await WhenIOpenTheTab("Suggestions", Project);
            var form = Gap(title).QuerySelector("form.dismiss-gap") ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be dismissed.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("the risk {string} gets the action {string}")]
        public Task WhenTheRiskGetsTheAction(string risk, string action) => _actions.WhenIAddTheActionDue(action, risk, "31-01-2027");

        [Then("the category {string} lists {string}")]
        public void ThenTheCategoryLists(string category, string title)
            => Assert.Contains(Category(category).QuerySelectorAll(".gap"), g => g.GetAttribute("data-title")!.StartsWith(title, StringComparison.Ordinal));

        [Then("the category {string} lists {string} because {string}")]
        public void ThenTheCategoryListsBecause(string category, string title, string missing)
        {
            var gap = Category(category).QuerySelectorAll(".gap").Single(g => g.GetAttribute("data-title") == title);
            Assert.Equal(missing, gap.QuerySelector(".gap-missing")?.TextContent.Trim());
        }

        [Then("the category {string} does not list {string}")]
        public async Task ThenTheCategoryDoesNotList(string category, string title)
        {
            await WhenIOpenTheTab("Suggestions", Project);
            Assert.DoesNotContain(Category(category).QuerySelectorAll(".gap"), g => g.GetAttribute("data-title")!.StartsWith(title, StringComparison.Ordinal));
        }

        [Then("the category {string} says {string}")]
        public void ThenTheCategorySays(string category, string text)
            => Assert.StartsWith(text, Category(category).QuerySelector(".gap-empty")?.TextContent.Trim() ?? "");

        [Then("the gap {string} offers to add a risk")]
        public void ThenTheGapOffersToAddARisk(string title)
        {
            var link = Gap(title).QuerySelector("a.add-risk-from-gap")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No \"Add a risk\".");
            Assert.Contains("title=" + Uri.EscapeDataString(title).Replace("%20", "%20"), link.Replace("+", "%20"));
        }

        [Then("no gap can be dismissed")]
        public void ThenNoGapCanBeDismissed() => Assert.Empty(_web.Page!.QuerySelectorAll("form.dismiss-gap"));

        private IElement Category(string category)
            => _web.Page!.QuerySelector($".gap-category[data-category='{category}']") ?? throw new Xunit.Sdk.XunitException($"No category '{category}'.");

        private IElement Gap(string title)
            => _web.Page!.QuerySelectorAll(".gap").FirstOrDefault(g => g.GetAttribute("data-title")!.StartsWith(title, StringComparison.Ordinal))
               ?? throw new Xunit.Sdk.XunitException($"No gap '{title}'.");
    }
}
