using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-220: the Link page (/Projects/Details/{id}/link). UI contract: Views/Projects/_LinkTab.cshtml (#link-requirements
    // with a.link-requirement-choice[data-code][data-mark][data-covered], a.link-filter[data-filter].active, #link-stories
    // and #link-evidence with li.link-item[data-title][data-state] and the forms .link-link, .link-approve, .link-unlink).
    // Every form is sent as the button sends it, with its hidden fields.
    [Binding]
    public class LinkPageStepDefinitions
    {
        private readonly WebScenario _web;

        public LinkPageStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the Link page of {string}")]
        public async Task WhenIOpenTheLinkPage(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/link");

        // The requirement is chosen as a person chooses it: from the left column, with every requirement shown
        [When("I open the Link page of {string} and choose {string}")]
        public async Task WhenIOpenAndChoose(string project, string code)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/link?show=all");
            var choice = Choice(code) ?? throw new Xunit.Sdk.XunitException($"The Link page does not list '{code}'.");
            await _web.OpenAsync(choice.GetAttribute("href")!);
            Assert.Equal(code, Chosen());
        }

        [When("I link the product requirement {string} on the Link page")]
        public Task WhenILinkTheStory(string title) => SendAsync(Item("link-stories", title, "other"), "form.link-link");

        [When("I link the evidence {string} on the Link page")]
        public Task WhenILinkTheEvidence(string title) => SendAsync(Item("link-evidence", title, "other"), "form.link-link");

        [When("I click \"Link\" on the suggested product requirement {string}")]
        public Task WhenIClickLinkOnTheSuggestion(string title) => SendAsync(Item("link-stories", title, "suggested"), "form.link-approve");

        // US-228 R2: the reason is left empty, so the rule is broken
        [When("I reject the suggested product requirement {string} without a reason")]
        public Task WhenIRejectWithoutAReason(string title) => SendAsync(Item("link-stories", title, "suggested"), "form.link-reject");

        [Then("the product requirements column shows {string} as {string}")]
        public void ThenTheStoriesColumnShows(string title, string state) => Assert.NotNull(Item("link-stories", title, state));

        [Then("the evidence column shows {string} as {string}")]
        public void ThenTheEvidenceColumnShows(string title, string state) => Assert.NotNull(Item("link-evidence", title, state));

        [Then("the Link page still has {string} chosen")]
        public void ThenTheLinkPageStillHasChosen(string code)
        {
            Assert.NotNull(_web.Page!.QuerySelector("#link-page"));
            Assert.Equal(code, Chosen());
        }

        [Then("the requirement {string} is marked {string} on the Link page")]
        public void ThenTheRequirementIsMarked(string code, string mark)
            => Assert.Equal(mark, (Choice(code) ?? throw new Xunit.Sdk.XunitException($"'{code}' is not listed.")).GetAttribute("data-mark"));

        [Then("the requirement {string} has the evidence {string}")]
        public async Task ThenTheRequirementHasTheEvidence(string code, string title)
        {
            await using var db = _web.CreateDbContext();
            var requirementIds = await db.Requirements.Where(r => r.Code == code).Select(r => r.Id).ToListAsync();
            var evidenceId = await db.Evidence.Where(e => e.Title == title).Select(e => e.Id).SingleAsync();
            Assert.True(await db.EvidenceLinks.AnyAsync(l => l.EvidenceId == evidenceId && requirementIds.Contains(l.RequirementId) && l.RemovedAt == null),
                $"'{title}' is not linked to '{code}'.");
        }

        [Then("the Link page's filter {string} is chosen")]
        public void ThenTheFilterIsChosen(string filter)
            => Assert.Equal(filter, _web.Page!.QuerySelector("#link-requirements a.link-filter.active")?.GetAttribute("data-filter"));

        [Then("every requirement shown on the Link page is not covered")]
        public void ThenEveryRequirementIsNotCovered()
        {
            var shown = _web.Page!.QuerySelectorAll("#link-requirements a.link-requirement-choice");
            Assert.NotEmpty(shown);
            Assert.All(shown, a => Assert.Equal("false", a.GetAttribute("data-covered")));
        }

        [Then("the requirement {string} is not shown on the Link page")]
        public void ThenTheRequirementIsNotShown(string code) => Assert.Null(Choice(code));

        private IElement? Choice(string code)
            => _web.Page!.QuerySelectorAll("#link-requirements a.link-requirement-choice").FirstOrDefault(a => a.GetAttribute("data-code") == code);

        private string? Chosen()
            => _web.Page!.QuerySelector("#link-requirements a.link-requirement-choice[aria-current='true']")?.GetAttribute("data-code");

        private IElement? Item(string column, string title, string state)
            => _web.Page!.QuerySelectorAll($"#{column} li.link-item")
                .FirstOrDefault(li => li.GetAttribute("data-title") == title && li.GetAttribute("data-state") == state);

        private async Task SendAsync(IElement? item, string selector)
        {
            var form = item?.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"No '{selector}' form on the Link page.");
            var fields = form.QuerySelectorAll("input[type='hidden']")
                .Where(i => i.GetAttribute("name") is not "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
