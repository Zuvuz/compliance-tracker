using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-208: the ISMS scope page (/Isms/Scope). UI contract: Views/Isms/Scope.cshtml (#scope-version as the status badge,
    // #scope-review-overdue, #scope-read-only, #scope-projects rows with data-project, data-state and .scope-reason,
    // #interested-parties items with data-party and .party-requirement with data-handled, form#describe-scope,
    // form#approve-scope, form#lock-scope, form#add-party, a#scope-pdf). The forms are sent to their actions with the ids
    // from the database, so a member without the permission can be tried too.
    [Binding]
    public class IsmsScopeStepDefinitions
    {
        private const string Path = "/Isms/Scope";
        private readonly WebScenario _web;
        private HttpResponseMessage? _download;

        public IsmsScopeStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I open the ISMS scope")]
        public Task WhenIOpenTheIsmsScope() => _web.OpenAsync(Path);

        [Given("I have described the ISMS scope as {string}")]
        public Task GivenIHaveDescribedTheScope(string text) => DescribeAsync(text, null);

        [Given("I have described the ISMS scope as {string} with the next review on {string}")]
        public Task GivenIHaveDescribedTheScopeWithReview(string text, string review) => DescribeAsync(text, review);

        [Given("I put {string} inside the ISMS scope")]
        [When("I put {string} inside the ISMS scope")]
        public async Task WhenIPutInside(string project)
            => await PostAsync("/Isms/Include", new() { ["projectId"] = (await ProjectIdAsync(project)).ToString() });

        [When("I put {string} outside the ISMS scope because {string}")]
        public async Task WhenIPutOutside(string project, string reason)
            => await PostAsync("/Isms/Exclude", new() { ["projectId"] = (await ProjectIdAsync(project)).ToString(), ["reason"] = reason });

        [Given("I approve the ISMS scope")]
        [When("I approve the ISMS scope")]
        public Task WhenIApprove() => PostAsync("/Isms/Approve", new());

        [When("I lock the ISMS scope")]
        public async Task WhenILock()
        {
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector("form#lock-scope"));
            await PostAsync("/Isms/LockScope", new());
        }

        [Given("I have added the interested party {string} of the kind {string} with the requirement {string} handled and {string} not handled")]
        [When("I add the interested party {string} of the kind {string} with the requirement {string} handled and {string} not handled")]
        public async Task WhenIAddTheInterestedParty(string name, string kind, string handled, string notHandled)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/AddParty", new List<KeyValuePair<string, string>>
            {
                new("name", name),
                new("kind", kind),
                new("requirement", handled),
                new("requirement", notHandled),
                new("requirement", ""),
                new("handled", "0")
            });
        }

        [When("I download the ISMS scope as PDF")]
        public async Task WhenIDownloadThePdf()
        {
            await _web.OpenAsync(Path);
            var link = _web.Page!.QuerySelector("a#scope-pdf")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No link to the PDF.");
            _download = await _web.Client.GetAsync(link);
        }

        // ---------------------------------------------------------- Then

        [Then("the ISMS scope shows {string} as {string}")]
        public async Task ThenTheScopeShows(string project, string state)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(state, Row(project).GetAttribute("data-state"));
        }

        [Then("the ISMS scope shows {string} as {string} with the reason {string}")]
        public async Task ThenTheScopeShowsWithReason(string project, string state, string reason)
        {
            await ThenTheScopeShows(project, state);
            Assert.Equal(reason, Row(project).QuerySelector(".scope-reason")?.TextContent.Trim());
        }

        [Then("the ISMS page says {string}")]
        public void ThenTheIsmsPageSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("the interested party {string} has the requirement {string} handled")]
        public Task ThenHandled(string party, string requirement) => AssertRequirementAsync(party, requirement, true);

        [Then("the interested party {string} has the requirement {string} not handled")]
        public Task ThenNotHandled(string party, string requirement) => AssertRequirementAsync(party, requirement, false);

        [Then("the ISMS scope is {string}")]
        public async Task ThenTheScopeIs(string status)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(status, _web.Page!.QuerySelector("#scope-version")?.TextContent.Trim());
        }

        [Then("the ISMS scope says the review is overdue")]
        public void ThenOverdue() => Assert.NotNull(_web.Page!.QuerySelector("#scope-review-overdue"));

        [Then("the ISMS scope is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#scope-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#describe-scope, form.include-project, form#add-party, form#approve-scope"));
        }

        [Then("the ISMS download is a PDF")]
        public async Task ThenTheDownloadIsAPdf()
        {
            Assert.Equal("application/pdf", _download!.Content.Headers.ContentType?.MediaType);
            var bytes = await _download.Content.ReadAsByteArrayAsync();
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        // ---------------------------------------------------------- Helpers

        private async Task DescribeAsync(string text, string? review)
        {
            await _web.OpenAsync(Path);
            var fields = new Dictionary<string, string> { ["text"] = text };
            if (review is not null)
            {
                fields["nextReviewOn"] = review;
            }

            await _web.PostFormAsync("/Isms/Describe", fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        // The form token comes from the page shown last (every page has the sign-out form)
        private async Task PostAsync(string path, Dictionary<string, string> fields)
        {
            if (_web.Page is null)
            {
                await _web.OpenAsync(Path);
            }

            await _web.PostFormAsync(path, fields);
        }

        private async Task<Guid> ProjectIdAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == name).Select(p => p.Id).SingleAsync();
        }

        private async Task AssertRequirementAsync(string party, string requirement, bool handled)
        {
            await _web.OpenAsync(Path);
            var item = _web.Page!.QuerySelector($"#interested-parties [data-party='{party}']")
                       ?? throw new Xunit.Sdk.XunitException($"No interested party '{party}'.");
            var line = item.QuerySelectorAll(".party-requirement").FirstOrDefault(l => l.TextContent.Trim().StartsWith(requirement, StringComparison.Ordinal))
                       ?? throw new Xunit.Sdk.XunitException($"{party} has no requirement '{requirement}'.");
            Assert.Equal(handled.ToString(), line.GetAttribute("data-handled"));
        }

        private AngleSharp.Dom.IElement Row(string project)
            => _web.Page!.QuerySelector($"#scope-projects tr[data-project='{project}']")
               ?? throw new Xunit.Sdk.XunitException($"No project '{project}' in the ISMS scope.");
    }
}
