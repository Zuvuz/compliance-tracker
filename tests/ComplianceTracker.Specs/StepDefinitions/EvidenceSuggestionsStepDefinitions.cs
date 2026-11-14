using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Security;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-058: "Upload test results" on the Evidence tab (#test-results-upload, #test-results-summary), and per Compliance
    // tab row .suggested-evidence (data-title) with forms .accept-evidence-suggestion and .reject-evidence-suggestion-row,
    // and .failing-tests. The pretend GitHub knows which pull request closed an issue (FakeIssue.ClosedBy).
    [Binding]
    public class EvidenceSuggestionsStepDefinitions
    {
        private const string Project = "Acme Portal";
        private const string Repository = "acme/portal";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;
        private readonly ComplianceTabStepDefinitions _compliance;

        public EvidenceSuggestionsStepDefinitions(WebScenario web, FakeGitHub github, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _github = github;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given

        [Given("{string} is closed in GitHub by the pull request {string}")]
        public void GivenIsClosedByThePullRequest(string story, string pullRequest)
        {
            var issue = _github.Repository(Repository).Single(i => i.Title == story);
            var (number, title) = ParsePullRequest(pullRequest);
            issue.State = "closed";
            issue.ClosedBy = (number, title);
        }

        [Given("the row {string} has the suggested evidence {string}")]
        public async Task GivenTheRowHasTheSuggestedEvidence(string code, string pullRequest)
        {
            GivenIsClosedByThePullRequest("[US-004] Sign in with Keycloak", pullRequest);
            await WhenTheProjectIsSynced();
            await ThenTheRowHasTheSuggestedEvidence(code, pullRequest);
        }

        [Given("I reject the suggested evidence {string} with the reason {string}")]
        public async Task GivenIRejectTheSuggestedEvidence(string title, string reason)
        {
            var form = await SuggestionFormAsync(title, "form.reject-evidence-suggestion-row");
            var fields = HiddenFields(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // ---------------------------------------------------------- When

        [Given("I have uploaded test results where {int} scenarios tagged {string} passed")]
        [When("I upload test results where {int} scenarios tagged {string} passed")]
        public Task WhenIUploadTestResults(int passed, string tag)
            => UploadAsync("results.trx", Trx(tag, passed, failed: 0));

        [When("I upload test results where {int} scenarios tagged {string} passed and {int} failed")]
        public Task WhenIUploadTestResultsWithFailures(int passed, string tag, int failed)
            => UploadAsync("results.trx", Trx(tag, passed, failed));

        [When("I upload JUnit results where {int} scenarios tagged {string} passed")]
        public Task WhenIUploadJUnitResults(int passed, string tag)
            => UploadAsync("results.xml", JUnit(tag, passed));

        [When("the project is synced with GitHub")]
        public async Task WhenTheProjectIsSynced()
        {
            var projectId = await ProjectIdAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}/product-requirements");
            await _web.PostFormAsync("/Projects/SyncNow", new Dictionary<string, string> { ["projectId"] = projectId.ToString() });
        }

        [When("I accept the suggested evidence {string}")]
        public async Task WhenIAcceptTheSuggestedEvidence(string title)
        {
            var form = await SuggestionFormAsync(title, "form.accept-evidence-suggestion");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        // ---------------------------------------------------------- Then

        [Then("the row {string} has the suggested evidence {string}")]
        public async Task ThenTheRowHasTheSuggestedEvidence(string code, string title)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            Assert.Contains(Suggested(code), s => (s.GetAttribute("data-title") ?? "").StartsWith(title, StringComparison.Ordinal));
        }

        [Then("the row {string} has no suggested evidence")]
        public async Task ThenTheRowHasNoSuggestedEvidence(string code)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            Assert.Empty(Suggested(code));
        }

        [Then("the row {string} shows {string}")]
        public void ThenTheRowShows(string code, string text)
            => Assert.Equal(text, Row(code).QuerySelector(".failing-tests")?.TextContent.Trim());

        [Then("the upload says {string}")]
        public void ThenTheUploadSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#test-results-summary")?.TextContent ?? "");

        [Then("the row {string} shows the evidence {string} as a file listing the {int} scenarios")]
        public async Task ThenTheRowShowsTheEvidenceAsAFile(string code, string titleStart, int scenarios)
        {
            var evidence = Row(code).QuerySelectorAll(".evidence")
                .FirstOrDefault(e => (e.GetAttribute("data-title") ?? "").StartsWith(titleStart, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' shows no evidence starting with '{titleStart}'.");
            var download = evidence.QuerySelector("a.evidence-title")!.GetAttribute("href")!;
            var file = await _web.Client.GetStringAsync(download);
            Assert.Equal(scenarios, file.Split('\n').Count(l => l.StartsWith("- Scenario ", StringComparison.Ordinal)));
            await using var db = _web.CreateDbContext();
            Assert.Contains(await db.EvidenceSuggestions.Select(s => s.ExternalId).FirstAsync(), file); // the results file's SHA-256
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string fileName, string content)
        {
            var projectId = await ProjectIdAsync();
            await _web.OpenAsync($"/Projects/Details/{projectId}/evidence");
            var form = _web.Page!.QuerySelector("#test-results-upload")
                ?? throw new Xunit.Sdk.XunitException("The Evidence tab has no test results upload.");
            var fields = HiddenFields(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, Encoding.UTF8.GetBytes(content));
        }

        // A TRX file like "dotnet test --logger trx" writes with xUnit: the tags only in the output (B45)
        private static string Trx(string tag, int passed, int failed)
        {
            var results = new StringBuilder();
            var tests = new StringBuilder();
            for (var i = 1; i <= passed + failed; i++)
            {
                var id = Guid.NewGuid();
                var outcome = i <= passed ? "Passed" : "Failed";
                results.Append($"""<UnitTestResult testId="{id}" testName="Scenario {i}" outcome="{outcome}"><Output><StdOut>Requirement tags: {SecurityElement.Escape(tag)} @US-004</StdOut></Output></UnitTestResult>""");
                tests.Append($"""<UnitTest name="Scenario {i}" id="{id}"><TestMethod className="Specs" name="Scenario{i}" /></UnitTest>""");
            }

            return $"""<?xml version="1.0" encoding="utf-8"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>{results}</Results><TestDefinitions>{tests}</TestDefinitions></TestRun>""";
        }

        // JUnit XML with the tags as properties
        private static string JUnit(string tag, int passed)
        {
            var cases = string.Concat(Enumerable.Range(1, passed).Select(i =>
                $"""<testcase name="Scenario {i}" classname="Specs"><properties><property name="Category" value="{SecurityElement.Escape(tag)}" /></properties></testcase>"""));
            return $"""<?xml version="1.0" encoding="utf-8"?><testsuites><testsuite name="Specs" tests="{passed}">{cases}</testsuite></testsuites>""";
        }

        private static (int Number, string Title) ParsePullRequest(string text)
        {
            // "PR 42 – Keycloak sign-in"
            var parts = text["PR ".Length..].Split(" – ", 2);
            return (int.Parse(parts[0]), parts[1]);
        }

        private async Task<IElement> SuggestionFormAsync(string title, string selector)
        {
            var code = await CodeOfSuggestionAsync(title);
            await _compliance.OpenSubTabWithAsync(Project, code);
            return Suggested(code).FirstOrDefault(s => (s.GetAttribute("data-title") ?? "").StartsWith(title, StringComparison.Ordinal))?.QuerySelector(selector)
                   ?? throw new Xunit.Sdk.XunitException($"No suggested evidence '{title}' with {selector}.");
        }

        private async Task<string> CodeOfSuggestionAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            var requirementId = await db.EvidenceSuggestions.Where(s => s.Title.StartsWith(title)).Select(s => s.RequirementId).FirstAsync();
            return await db.Requirements.Where(r => r.Id == requirementId).Select(r => r.Code).SingleAsync();
        }

        private IEnumerable<IElement> Suggested(string code) => Row(code).QuerySelectorAll(".suggested-evidence");

        private IElement Row(string code)
            => RequirementRowLookup.Find(_web, code)
               ?? throw new Xunit.Sdk.XunitException($"The page lists no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private async Task<Guid> ProjectIdAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
        }
    }
}
