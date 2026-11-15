using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-047: the checklist on the Overview tab: #checklist with .checklist-item (data-key, data-state), .checklist-text,
    // .checklist-source and form.checklist-change (data-change); the warning #checklist-warning in the phase form
    [Binding]
    public class PhaseChecklistStepDefinitions
    {
        private readonly WebScenario _web;
        private Guid _project;

        public PhaseChecklistStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // Set straight in the database, as the gates are tested in their own features (US-071, US-072)
        [Given("{string} is in the phase {string}")]
        public async Task GivenIsInThePhase(string project, string phase)
        {
            await using var db = _web.CreateDbContext();
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Projects" SET "Phase" = {phase} WHERE "Name" = {project}""");
            _project = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        [When("I tick the checklist item {string} of {string} with the comment {string}")]
        public Task WhenITick(string item, string project, string comment)
            => ChangeAsync(project, item, "tick", new() { ["comment"] = comment });

        [Given("I turn off the checklist item {string} of {string} because {string}")]
        [When("I turn off the checklist item {string} of {string} because {string}")]
        public Task WhenITurnOff(string item, string project, string reason)
            => ChangeAsync(project, item, "off", new() { ["reason"] = reason });

        [When("I add the optional test {string} to the checklist of {string}")]
        public Task WhenIAddTheOptionalTest(string item, string project)
            => ChangeAsync(project, item, "on", new());

        [Then("the checklist lists {string} for {string}")]
        public void ThenTheChecklistLists(string item, string source)
            => Assert.Equal(source, Item(item).QuerySelector(".checklist-source")?.TextContent.Trim());

        [Then("the checklist does not list {string}")]
        public void ThenTheChecklistDoesNotList(string item)
            => Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#checklist .checklist-text"), t => t.TextContent.Trim() == item);

        [Then("the checklist item {string} is {string}")]
        public async Task ThenTheItemIs(string item, string state)
        {
            await _web.OpenAsync($"/Projects/Details/{_project}");
            Assert.Equal(state, Item(item).GetAttribute("data-state"));
        }

        [Then("the phase form warns that {string} is missing")]
        public void ThenThePhaseFormWarns(string item)
            => Assert.Contains(item, _web.Page!.QuerySelector("form#change-phase #checklist-warning")?.TextContent ?? "(no warning)");

        private AngleSharp.Dom.IElement Item(string text)
            => _web.Page!.QuerySelectorAll("#checklist .checklist-item").FirstOrDefault(i => i.QuerySelector(".checklist-text")?.TextContent.Trim() == text)
               ?? throw new Xunit.Sdk.XunitException($"The checklist has no '{text}': {_web.Page.QuerySelector("#checklist")?.TextContent}");

        private async Task ChangeAsync(string project, string item, string change, Dictionary<string, string> fields)
        {
            await using var db = _web.CreateDbContext();
            _project = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{_project}");
            var form = Item(item).QuerySelector($"form.checklist-change[data-change='{change}']")
                ?? throw new Xunit.Sdk.XunitException($"'{item}' cannot be changed with '{change}'.");
            foreach (var input in form.QuerySelectorAll("input[type=hidden]"))
            {
                fields[input.GetAttribute("name")!] = input.GetAttribute("value") ?? "";
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }
    }
}
