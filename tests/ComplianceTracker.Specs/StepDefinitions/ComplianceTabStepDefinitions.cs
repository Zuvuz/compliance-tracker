using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-019, US-021, US-041: the Compliance tab, through the web app with test sign-in and a pretend GitHub.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (#compliance-tabs links with data-sub, #coverage,
    // #requirement-search, rows .requirement-row with data-code, .requirement-status, .linked-story,
    // forms .link-requirement and .unlink-requirement), #coverage-summary on Overview and
    // .work-item-requirements on the User stories tab.
    [Binding]
    public class ComplianceTabStepDefinitions
    {
        private const string Repository = "acme/portal";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public ComplianceTabStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        // ---------------------------------------------------------- Given

        // Uploads the framework and adds the requirements by hand (US-007 R5), then activates it
        [Given("the framework {string} has the active requirements {string} and {string}")]
        public async Task GivenTheFrameworkHasTheActiveRequirements(string frameworkName, string first, string second)
        {
            await _web.OpenAsync("/Frameworks");
            await _web.PostFileAsync("/Frameworks/Add", new Dictionary<string, string> { ["Name"] = frameworkName },
                "framework.pdf", TestFiles.Pdf());
            await AddRequirementsAndActivateAsync(first, second);
        }

        [Given("version 2 of {string} has the requirement {string}")]
        public async Task GivenVersion2HasTheRequirement(string frameworkName, string requirement)
        {
            await _web.OpenAsync("/Frameworks");
            var row = _web.Page!.QuerySelectorAll("#frameworks tbody tr")
                .Single(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == frameworkName);
            await _web.PostFileAsync("/Frameworks/UploadVersion",
                new Dictionary<string, string> { ["FrameworkId"] = row.GetAttribute("data-framework-id")! },
                "framework-v2.pdf", TestFiles.Pdf());
            await AddRequirementsAndActivateAsync(requirement);
        }

        // The story comes from the pretend GitHub, like a real one (US-018)
        [Given("the project {string} has the user story {string}")]
        public async Task GivenTheProjectHasTheUserStory(string projectName, string title)
        {
            var issues = _github.Repository(Repository);
            issues.Add(new FakeIssue
            {
                Number = issues.Count + 1,
                Title = title,
                Labels = ["user-story"],
                Body = FakeGitHub.TemplateBody(title)
            });

            var projectId = await ProjectIdAsync(projectName);
            await _web.OpenAsync($"/Projects/Details/{projectId}/user-stories");
            await _web.PostFormAsync("/Projects/LinkGitHub", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["repository"] = Repository,
                ["label"] = "user-story"
            });
            await _web.PostFormAsync("/Projects/SyncNow", new Dictionary<string, string> { ["projectId"] = projectId.ToString() });
        }

        [Given("the user story {string} is linked to the requirement {string}")]
        public async Task GivenTheUserStoryIsLinked(string story, string code)
        {
            await WhenIOpenTheSubTab("Security policy", "Acme Portal");
            await WhenILinkToTheRequirement(story, code);
        }

        // ---------------------------------------------------------- When

        // Opens the Compliance tab and clicks the sub-tab, like a user
        [When("I open the {string} sub-tab of {string}")]
        public async Task WhenIOpenTheSubTab(string subTab, string projectName)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}/compliance");
            var link = SubTabs().FirstOrDefault(a => a.TextContent.Trim() == subTab)
                ?? throw new Xunit.Sdk.XunitException($"The Compliance tab has no sub-tab '{subTab}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I search the requirements for {string}")]
        public Task WhenISearchTheRequirements(string text)
            => SubmitSearchAsync(text, onlyNotCovered: false);

        [When("I show only \"Not covered\"")]
        public Task WhenIShowOnlyNotCovered()
            => SubmitSearchAsync("", onlyNotCovered: true);

        // Chooses the story in the row's "Link" form and sends it
        [When("I link {string} to the requirement {string}")]
        public async Task WhenILinkToTheRequirement(string story, string code)
        {
            var form = Row(code).QuerySelector("form.link-requirement")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no Link form.");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim() == story)
                ?? throw new Xunit.Sdk.XunitException($"'{story}' cannot be chosen for '{code}'.");

            var fields = HiddenFields(form);
            fields["workItemId"] = option.GetAttribute("value")!;
            await _web.PostFormAsync("/Projects/LinkRequirement", fields);
        }

        [When("I remove the link between {string} and {string}")]
        public async Task WhenIRemoveTheLink(string story, string code)
        {
            var form = Row(code).QuerySelectorAll("form.unlink-requirement")
                .FirstOrDefault(f => f.QuerySelector("button")?.GetAttribute("aria-label") == $"Remove link to {story}")
                ?? throw new Xunit.Sdk.XunitException($"'{code}' has no link to '{story}' to remove.");
            await _web.PostFormAsync("/Projects/UnlinkRequirement", HiddenFields(form));
        }

        // ---------------------------------------------------------- Then

        [Then("the table lists {string} and {string}")]
        public void ThenTheTableLists(string first, string second)
            => Assert.Equal([first, second], Codes());

        [Then("the table lists only {string}")]
        public void ThenTheTableListsOnly(string code)
            => Assert.Equal([code], Codes());

        [Then("the row {string} is {string} by {string}")]
        public void ThenTheRowIsBy(string code, string status, string story)
        {
            var row = Row(code);
            Assert.Equal(status, row.QuerySelector(".requirement-status")?.TextContent.Trim());
            Assert.Contains(story, row.QuerySelectorAll(".linked-story").Select(a => a.TextContent.Trim()));
        }

        [Then("the row {string} is {string}")]
        public void ThenTheRowIs(string code, string status)
            => Assert.Equal(status, Row(code).QuerySelector(".requirement-status")?.TextContent.Trim());

        [Then("the coverage says {string}")]
        public void ThenTheCoverageSays(string text)
            => Assert.Equal(text, _web.Page!.QuerySelector("#coverage")?.TextContent.Trim());

        [Then("the user story {string} shows the requirements {string}")]
        public void ThenTheUserStoryShowsTheRequirements(string story, string codes)
        {
            var item = _web.Page!.QuerySelectorAll(".work-item")
                .Single(i => i.QuerySelector(".work-item-title")?.TextContent.Trim() == story);
            Assert.Equal(codes, item.QuerySelector(".work-item-requirements")?.TextContent.Trim());
        }

        [Then("the removed link between {string} and {string} is still stored")]
        public async Task ThenTheRemovedLinkIsStillStored(string story, string code)
        {
            await using var db = _web.CreateDbContext();
            var removed = await (
                    from link in db.RequirementLinks
                    join requirement in db.Set<Domain.Frameworks.Requirement>() on link.RequirementId equals requirement.Id
                    join item in db.WorkItems on link.WorkItemId equals item.Id
                    where requirement.Code == code && item.Title == story && link.RemovedAt != null
                    select link)
                .CountAsync();
            Assert.Equal(1, removed);
        }

        [Then("I see the sub-tabs {string} and {string}")]
        public void ThenISeeTheSubTabs(string first, string second)
            => Assert.Equal([first, second], SubTabs().Select(a => a.TextContent.Trim()));

        [Then("the {string} sub-tab is active")]
        public void ThenTheSubTabIsActive(string subTab)
            => Assert.Equal([subTab], SubTabs().Where(a => a.ClassList.Contains("active")).Select(a => a.TextContent.Trim()));

        [Then("the coverage summary shows {string}")]
        public void ThenTheCoverageSummaryShows(string text)
        {
            var lines = _web.Page!.QuerySelectorAll("#coverage-summary li")
                .Select(li => string.Join(" ", li.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            Assert.Contains(text, lines);
        }

        // ---------------------------------------------------------- Helpers

        // On the draft page that is open after an upload: one requirement per "CODE Title", then activate
        private async Task AddRequirementsAndActivateAsync(params string[] requirements)
        {
            var versionId = FrameworkUi.OpenVersionId(_web);
            foreach (var requirement in requirements)
            {
                var space = requirement.IndexOf(' ');
                await FrameworkUi.AddRequirementAsync(_web, versionId, requirement[..space], requirement[(space + 1)..]);
            }

            await FrameworkUi.ActivateAsync(_web, versionId);
        }

        private async Task SubmitSearchAsync(string text, bool onlyNotCovered)
        {
            var action = _web.Page!.QuerySelector("#requirement-search")!.GetAttribute("action")!;
            var query = $"?q={Uri.EscapeDataString(text)}" + (onlyNotCovered ? "&show=not-covered" : "");
            await _web.OpenAsync(action + query);
        }

        private IEnumerable<IElement> SubTabs() => _web.Page!.QuerySelectorAll("#compliance-tabs a[data-sub]");

        private IReadOnlyList<string> Codes()
            => _web.Page!.QuerySelectorAll("#requirements-table tr.requirement-row").Select(r => r.GetAttribute("data-code")!).ToList();

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
