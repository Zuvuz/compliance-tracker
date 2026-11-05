using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-107: the link form #link-github (repository holds the address, token), #choose-github-project with
    // .github-project-choice buttons, and #linked-github-project. The pretend GitHub (FakeGitHub.Organizations) answers GraphQL.
    [Binding]
    public class GitHubProjectsStepDefinitions
    {
        private const string Organisation = "acme";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public GitHubProjectsStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        // ---------------------------------------------------------- Given

        // "3 Customer portal": the number, then the title
        [Given("the GitHub organisation {string} has the projects {string} and {string}")]
        public void GivenTheOrganisationHasTheProjects(string organisation, string first, string second)
            => _github.Organizations[organisation] = [Project(first), Project(second)];

        // Each project's issues are in a repository of their own, so the numbers do not collide
        [Given("{string} has the user story {string}")]
        public void GivenTheProjectHasTheUserStory(string projectTitle, string story)
        {
            var project = _github.Organizations[Organisation].Single(p => p.Title == projectTitle);
            var repository = $"{Organisation}/project-{project.Number}";
            project.Items.Add((repository, NewIssue(repository, story)));
        }

        [Given("the repository {string} has the user story {string}")]
        public void GivenTheRepositoryHasTheUserStory(string repository, string story)
            => NewIssue(repository, story);

        [Given("{string} is linked to the GitHub Project {string}")]
        public Task GivenIsLinked(string project, string address)
            => LinkAsync(project, address, FakeGitHub.ProjectsToken);

        // ---------------------------------------------------------- When

        [When("I link {string} to the GitHub Project {string} with a token")]
        [When("I enter the GitHub address {string} for {string} with a token")]
        public Task WhenILinkWithAToken(string first, string second)
        {
            // The two sentences name the project and the address in a different order
            var (project, address) = first.Contains("github.com", StringComparison.Ordinal) || first.Contains('/') ? (second, first) : (first, second);
            return LinkAsync(project, address, FakeGitHub.ProjectsToken);
        }

        [When("I link {string} to the GitHub Project {string} without a token")]
        public Task WhenILinkWithoutAToken(string project, string address)
            => LinkAsync(project, address, null);

        [When("I choose {string}")]
        public async Task WhenIChoose(string choice)
        {
            var number = choice.Split(' ')[0];
            var form = _web.Page!.QuerySelectorAll("#choose-github-project form")
                .Single(f => f.QuerySelector("input[name=number]")!.GetAttribute("value") == number);
            await _web.PostFormAsync(form.GetAttribute("action")!,
                form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? ""));
        }

        // ---------------------------------------------------------- Then

        [Then("I can choose between {string} and {string}")]
        public void ThenICanChooseBetween(string first, string second)
            => Assert.Equal(new[] { first, second },
                _web.Page!.QuerySelectorAll(".github-project-choice").Select(b => b.TextContent.Trim()));

        [Then("the page says the project reads {string}")]
        public void ThenThePageSaysTheProjectReads(string title)
            => Assert.Contains(title, _web.Page!.QuerySelector("#linked-github-project")?.TextContent ?? "(not linked to a GitHub Project)");

        [Then("{string} has the user story {string}")]
        public async Task ThenHasTheUserStory(string project, string story)
            => Assert.Contains(story, await StoriesAsync(project, removed: false));

        [Then("{string} does not have the user story {string}")]
        public async Task ThenDoesNotHaveTheUserStory(string project, string story)
            => Assert.DoesNotContain(story, await StoriesAsync(project, removed: false));

        [Then("the user story {string} of {string} is {string}")]
        public async Task ThenTheUserStoryIs(string story, string project, string state)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.Equal(Enum.Parse<WorkItemState>(state),
                await db.WorkItems.Where(w => w.ProjectId == projectId && w.Title == story).Select(w => w.State).SingleAsync());
        }

        // ---------------------------------------------------------- Helpers

        private static FakeProject Project(string text)
        {
            var space = text.IndexOf(' ');
            return new FakeProject { Number = int.Parse(text[..space]), Title = text[(space + 1)..] };
        }

        private FakeIssue NewIssue(string repository, string story)
        {
            var issues = _github.Repository(repository);
            var issue = new FakeIssue { Number = issues.Count + 1, Title = story, Labels = ["user-story"], Body = FakeGitHub.TemplateBody(story) };
            issues.Add(issue);
            return issue;
        }

        // The form on the Product requirements tab, like a user; then a sync, as "Sync now" does
        private async Task LinkAsync(string project, string address, string? token)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}/product-requirements");
            var fields = new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["repository"] = address,
                ["label"] = "user-story"
            };
            if (token is not null)
            {
                fields["token"] = token;
            }

            await _web.PostFormAsync("/Projects/LinkGitHub", fields);
            var errors = _web.Page!.QuerySelectorAll(".validation-summary-errors li").Any();
            if (!errors && address.Count(c => c == '/') == 1 && !address.Contains("github.com", StringComparison.Ordinal))
            {
                // a repository is read at the next sync (US-018)
                await _web.PostFormAsync("/Projects/SyncNow", new Dictionary<string, string> { ["projectId"] = id.ToString() });
            }
        }

        private async Task<List<string>> StoriesAsync(string project, bool removed)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            return await db.WorkItems.Where(w => w.ProjectId == projectId && (removed || w.State != WorkItemState.Removed))
                .Select(w => w.Title).ToListAsync();
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
