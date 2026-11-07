using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-122: the upload API, called as a pipeline does: a client without sign-in or cookies, only the bearer token.
    // UI contract: Views/Projects/_ScansTab.cshtml (#create-upload-token, #new-upload-token, .upload-token rows with
    // .token-last-used and the form .revoke-upload-token, .scan-build on each latest scan).
    [Binding]
    public class PipelineScanUploadStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private readonly Dictionary<string, string> _tokens = new();
        private HttpResponseMessage? _answer;
        private string? _project;

        public PipelineScanUploadStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        [Given("I have created the upload token {string} for {string}")]
        public async Task GivenIHaveCreatedTheToken(string name, string project)
        {
            _project = project;
            await OpenScansAsync(project);
            var form = _web.Page!.QuerySelector("#create-upload-token")
                ?? throw new Xunit.Sdk.XunitException("The Scans tab has no form to create an upload token.");
            var fields = Hidden(form);
            fields["name"] = name;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            _tokens[project] = _web.Page!.QuerySelector("#new-upload-token")?.TextContent.Trim()
                ?? throw new Xunit.Sdk.XunitException("The new token was not shown.");
        }

        [Given("I have revoked the upload token {string}")]
        public async Task GivenIHaveRevoked(string name)
        {
            await OpenScansAsync(_project!);
            var form = Token(name).QuerySelector("form.revoke-upload-token")
                ?? throw new Xunit.Sdk.XunitException($"'{name}' cannot be revoked.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Given("the pipeline has uploaded {int} scans within the hour")]
        public async Task GivenUploadedScans(int count)
        {
            for (var i = 0; i < count; i++)
            {
                await UploadAsync(_project!, _tokens[_project!], "scan.sarif", TestScans.Findings("Trivy", 1, 0), null, null);
                Assert.Equal(201, (int)_answer!.StatusCode);
            }
        }

        [When("the pipeline uploads a {string} scan with {int} findings for commit {string} and version {string}")]
        public Task WhenUploadsWithCommit(string tool, int findings, string commit, string version)
            => UploadAsync(_project!, _tokens[_project!], "trivy.sarif", TestScans.Findings(tool, findings, 0), commit, version);

        [When("the pipeline uploads a {string} scan with {int} findings")]
        public Task WhenUploads(string tool, int findings)
            => UploadAsync(_project!, _tokens[_project!], "trivy.sarif", TestScans.Findings(tool, findings, 0), null, null);

        [When("the pipeline uploads a {string} scan to {string} with the token of {string}")]
        public Task WhenUploadsToOther(string tool, string project, string tokenOf)
            => UploadAsync(project, _tokens[tokenOf], "trivy.sarif", TestScans.Findings(tool, 1, 0), null, null);

        [When("the pipeline uploads a {string} scan with the token {string}")]
        public Task WhenUploadsWithToken(string tool, string token)
            => UploadAsync(_project!, token, "trivy.sarif", TestScans.Findings(tool, 1, 0), null, null);

        [When("the pipeline uploads a {string} scan without a token")]
        public Task WhenUploadsWithoutToken(string tool)
            => UploadAsync(_project!, null, "trivy.sarif", TestScans.Findings(tool, 1, 0), null, null);

        [When("the pipeline uploads the file {string} with {string}")]
        public Task WhenUploadsTheFile(string fileName, string content)
            => UploadAsync(_project!, _tokens[_project!], fileName, Encoding.UTF8.GetBytes(content), null, null);

        [Then("the answer is {int}")]
        public void ThenTheAnswerIs(int status) => Assert.Equal(status, (int)_answer!.StatusCode);

        [Then("the answer is {int} with {string}")]
        public async Task ThenTheAnswerIsWith(int status, string text)
        {
            ThenTheAnswerIs(status);
            using var json = JsonDocument.Parse(await _answer!.Content.ReadAsStringAsync());
            var message = json.RootElement.TryGetProperty("summary", out var summary) ? summary.GetString() : json.RootElement.GetProperty("error").GetString();
            Assert.Equal(text, message);
        }

        [Then("the scan shows {string}")]
        public async Task ThenTheScanShows(string build)
        {
            await OpenScansAsync(_project!);
            Assert.Contains(build, _web.Page!.QuerySelectorAll("#latest-scans .scan-build").Select(e => e.TextContent.Trim()));
        }

        [Then("the upload token {string} was last used today")]
        public async Task ThenLastUsedToday(string name)
        {
            await OpenScansAsync(_project!);
            Assert.StartsWith(_clock.GetUtcNow().UtcDateTime.ToLocalTime().ToString("dd-MM-yyyy"), Token(name).QuerySelector(".token-last-used")!.TextContent.Trim());
        }

        // R2: the next page no longer shows it
        [Then("the token was shown once")]
        public async Task ThenShownOnce()
        {
            Assert.StartsWith("ctu_", _tokens[_project!]);
            await OpenScansAsync(_project!);
            Assert.Null(_web.Page!.QuerySelector("#new-upload-token"));
            Assert.DoesNotContain(_tokens[_project!], _web.Page!.DocumentElement.OuterHtml);
        }

        [Then("the database keeps only the hash of the token")]
        public async Task ThenOnlyTheHash()
        {
            await using var db = _web.CreateDbContext();
            var stored = await db.ScanUploadTokens.SingleAsync();
            Assert.Equal(ComplianceTracker.Domain.Scanning.ScanUploadToken.HashOf(_tokens[_project!]), stored.Hash);
            Assert.DoesNotContain(_tokens[_project!], string.Join('|', await db.ActivityLog.Select(e => e.Details ?? "").ToListAsync()));
        }

        // ---------------------------------------------------------- Helpers

        // Like curl -H "Authorization: Bearer …" -F file=@… -F commit=… -F version=…; no cookie, no antiforgery
        private async Task UploadAsync(string project, string? token, string fileName, byte[] content, string? commit, string? version)
        {
            using var pipeline = _web.CreateClient(followRedirects: false);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{await ProjectIdAsync(project)}/scans");
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/sarif+json");
            form.Add(file, "file", fileName);
            if (commit is not null)
            {
                form.Add(new StringContent(commit), "commit");
            }

            if (version is not null)
            {
                form.Add(new StringContent(version), "version");
            }

            request.Content = form;
            _answer = await pipeline.SendAsync(request);
        }

        private async Task OpenScansAsync(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/scans");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private IElement Token(string name)
            => _web.Page!.QuerySelectorAll("#upload-tokens .upload-token").FirstOrDefault(r => r.GetAttribute("data-name") == name)
               ?? throw new Xunit.Sdk.XunitException($"The Scans tab shows no upload token '{name}'.");

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
