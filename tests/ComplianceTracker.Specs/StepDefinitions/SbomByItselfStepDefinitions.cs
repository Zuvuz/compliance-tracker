using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-148: SBOMs from a pipeline (POST /api/projects/{id}/sboms) and from GitHub's dependency graph. UI contract:
    // Views/Projects/_ScansTab.cshtml (#sboms rows .sbom[data-version] with .sbom-summary and .sbom-source,
    // form #fetch-sbom-from-github, #sbom-unavailable).
    [Binding]
    public class SbomByItselfStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;
        private readonly PipelineScanUploadStepDefinitions _pipeline;

        public SbomByItselfStepDefinitions(WebScenario web, FakeGitHub github, PipelineScanUploadStepDefinitions pipeline)
        {
            _web = web;
            _github = github;
            _pipeline = pipeline;
        }

        [When("the pipeline sends a CycloneDX SBOM with {int} components for version {string} with the token")]
        public Task WhenThePipelineSends(int count, string version) => _pipeline.SendSbomAsync(Project, withToken: true, TestSboms.CycloneDx(count), version);

        [When("the pipeline sends a CycloneDX SBOM with {int} components without a version")]
        public Task WhenThePipelineSendsWithoutVersion(int count) => _pipeline.SendSbomAsync(Project, withToken: true, TestSboms.CycloneDx(count), null);

        [Given("GitHub has an SBOM with {int} packages for {string}")]
        public void GivenGitHubHasAnSbom(int count, string repository) => _github.Sboms[repository] = TestSboms.Spdx(count);

        [When("I fetch the SBOM from GitHub for {string}")]
        public async Task WhenIFetchTheSbom(string project)
        {
            await OpenScansAsync(project);
            var form = _web.Page!.QuerySelector("form#fetch-sbom-from-github") ?? throw new Xunit.Sdk.XunitException("The Scans tab has no \"Fetch SBOM from GitHub\".");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["projectId"] = (await ProjectIdAsync(project)).ToString() });
        }

        [Then("the answer says {string}")]
        public void ThenTheAnswerSays(string text) => Assert.Contains(text, _pipeline.AnswerText ?? "(no answer)", StringComparison.Ordinal);

        [Then("the SBOM for version {string} comes from {string}")]
        public async Task ThenTheSbomComesFrom(string version, string source)
            => Assert.Equal(source, (await RowAsync(version)).QuerySelector(".sbom-source")?.TextContent.Trim());

        [Then("the Scans tab says {string}")]
        public void ThenTheScansTabSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#sbom-unavailable")?.TextContent ?? "(no message)", StringComparison.Ordinal);

        private async Task<IElement> RowAsync(string version)
        {
            await OpenScansAsync(Project);
            return _web.Page!.QuerySelectorAll("#sboms tr.sbom").FirstOrDefault(r => r.GetAttribute("data-version") == version)
                ?? throw new Xunit.Sdk.XunitException($"No SBOM for version '{version}'.");
        }

        private async Task OpenScansAsync(string project) => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/scans");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
