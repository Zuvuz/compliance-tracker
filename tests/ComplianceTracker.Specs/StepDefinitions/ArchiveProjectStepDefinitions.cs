using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-028: the project header's form#archive-project and #project-archived with form#restore-project
    // (Views/Projects/_ProjectHeader.cshtml), and the projects page's #project-list .project-row[data-project] with
    // .project-archived and a#show-archived (Views/Projects/Index.cshtml).
    [Binding]
    public class ArchiveProjectStepDefinitions
    {
        private readonly WebScenario _web;

        public ArchiveProjectStepDefinitions(WebScenario web) => _web = web;

        private async Task<Guid> ProjectIdAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == name).Select(p => p.Id).SingleAsync();
        }

        private async Task SendAsync(string project, string formId)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            var form = _web.Page!.QuerySelector($"form#{formId}") ?? throw new Xunit.Sdk.XunitException($"The page of {project} has no {formId}.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["projectId"] = form.QuerySelector("input[name=projectId]")!.GetAttribute("value")!
            });
        }

        [Given("I archive {string}")]
        [When("I archive {string}")]
        public Task WhenIArchive(string project) => SendAsync(project, "archive-project");

        [When("I restore {string}")]
        public Task WhenIRestore(string project) => SendAsync(project, "restore-project");

        [Then("the organisation page does not list the project {string}")]
        public async Task ThenNotListed(string project)
        {
            await _web.OpenAsync("/Projects");
            Assert.DoesNotContain(project, _web.Page!.QuerySelectorAll("#project-list .project-row").Select(r => r.GetAttribute("data-project")));
        }

        [Then("there is no Delete button on {string}")]
        public async Task ThenNoDelete(string project)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("button, a"), b => b.TextContent.Trim() == "Delete");
            Assert.NotNull(_web.Page!.QuerySelector("form#archive-project"));
        }

        [When("I show archived projects")]
        public async Task WhenIShowArchived()
        {
            await _web.OpenAsync("/Projects");
            var link = _web.Page!.QuerySelector("a#show-archived") ?? throw new Xunit.Sdk.XunitException("No \"Show archived\".");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the organisation page lists the project {string} marked {string}")]
        public void ThenListedMarked(string project, string mark)
        {
            var row = _web.Page!.QuerySelectorAll("#project-list .project-row").SingleOrDefault(r => r.GetAttribute("data-project") == project)
                ?? throw new Xunit.Sdk.XunitException($"{project} is not listed.");
            Assert.Equal(mark, row.QuerySelector(".project-archived")?.TextContent.Trim());
        }

        [Then("the page of {string} says it was archived by {string}")]
        public async Task ThenArchivedBy(string project, string name)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            var banner = _web.Page!.QuerySelector("#project-archived")?.TextContent ?? "";
            Assert.Matches(@"Archived on \d{2}-\d{2}-\d{4} by " + name, banner);
            Assert.Null(_web.Page!.QuerySelector("form#archive-project"));
            Assert.Null(_web.Page!.QuerySelector("#edit-project-link"));
        }
    }
}
