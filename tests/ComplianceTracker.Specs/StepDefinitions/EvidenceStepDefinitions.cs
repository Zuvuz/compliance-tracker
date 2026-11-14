using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-057: evidence on the Compliance tab, through the web app with test sign-in.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (#summary-evidence, rows .requirement-row with .evidence[data-title],
    // .evidence-title, .evidence-sha256, .evidence-expired, .evidence-status, forms .add-evidence and .remove-evidence,
    // and the "+ Evidence" summary).
    [Binding]
    public class EvidenceStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly Dictionary<string, byte[]> _files = new();

        public EvidenceStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- When

        [Given("I have added the evidence {string} with the link {string} to {string}")]
        [When("I add the evidence {string} with the link {string} to {string}")]
        public Task WhenIAddALink(string title, string url, string code)
            => AddAsync(code, title, url: url);

        [When("I add the evidence {string} with the link {string} valid until {string} to {string}")]
        public Task WhenIAddALinkValidUntil(string title, string url, string validUntil, string code)
            => AddAsync(code, title, url: url, validUntil: DateOnly.ParseExact(validUntil, "dd-MM-yyyy", CultureInfo.InvariantCulture));

        // A small PDF with the scenario's title in it, kept so the download can be compared
        [Given("I have added the evidence {string} with the file {string} to {string}")]
        [When("I add the evidence {string} with the file {string} to {string}")]
        public Task WhenIAddAFile(string title, string fileName, string code)
        {
            var content = Encoding.ASCII.GetBytes($"%PDF-1.7\n% {title}\n");
            _files[title] = content;
            return AddAsync(code, title, fileName: fileName, content: content);
        }

        [When("I add the evidence {string} with a file of {int} MB to {string}")]
        public Task WhenIAddALargeFile(string title, int megabytes, string code)
        {
            var content = new byte[megabytes * 1024 * 1024];
            Encoding.ASCII.GetBytes("%PDF-").CopyTo(content, 0);
            return AddAsync(code, title, fileName: "dump.pdf", content: content);
        }

        [When("I remove the evidence {string} from {string}")]
        public async Task WhenIRemoveTheEvidence(string title, string code)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            var form = Evidence(code, title).QuerySelector("form.remove-evidence")
                ?? throw new Xunit.Sdk.XunitException($"The evidence '{title}' cannot be removed.");
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("{string} downloads the file of {string}")]
        public async Task WhenDownloadsTheFile(string userName, string title)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync(await DownloadPathAsync(title));
        }

        // ---------------------------------------------------------- Then

        [Then("the row {string} shows the evidence {string}")]
        public void ThenTheRowShowsTheEvidence(string code, string title)
            => Evidence(code, title);

        [Then("the row {string} shows the evidence {string} with its SHA-256")]
        public void ThenTheRowShowsTheEvidenceWithItsSha256(string code, string title)
        {
            var sha = Convert.ToHexString(SHA256.HashData(_files[title])).ToLowerInvariant();
            var shown = Evidence(code, title).QuerySelector(".evidence-sha256")?.GetAttribute("title");
            Assert.Equal($"SHA-256 {sha}", shown);
        }

        [Then("the row {string} shows the evidence {string} as {string}")]
        public void ThenTheRowShowsTheEvidenceAs(string code, string title, string mark)
            => Assert.Equal(mark, Evidence(code, title).QuerySelector(".evidence-expired")?.TextContent.Trim());

        [Then("the file of {string} can be downloaded unchanged")]
        public async Task ThenTheFileCanBeDownloadedUnchanged(string title)
        {
            var link = _web.Page!.QuerySelectorAll(".evidence")
                .First(e => e.GetAttribute("data-title") == title)
                .QuerySelector("a.evidence-title")!;
            var response = await _web.Client.GetAsync(link.GetAttribute("href")!);

            Assert.True(response.IsSuccessStatusCode, $"The download gave {response.StatusCode}.");
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(_files[title], await response.Content.ReadAsByteArrayAsync());
        }

        [Then("the row {string} is {string} without evidence")]
        public void ThenTheRowIsWithoutEvidence(string code, string status)
        {
            var row = Row(code);
            Assert.Equal(status, row.QuerySelector(".requirement-status")?.TextContent.Trim());
            Assert.Equal("No evidence", row.QuerySelector(".evidence-status")?.TextContent.Trim());
        }

        [Then("the summary shows {int} with evidence")]
        public async Task ThenTheSummaryShowsWithEvidence(int count)
        {
            if (_web.Page!.QuerySelector("#summary-evidence") is null)
            {
                await _compliance.WhenIOpenTheSubTab("CRA", Project);
            }

            Assert.Equal(count.ToString(), _web.Page!.QuerySelector("#summary-evidence")?.TextContent.Trim());
        }

        [Then("the row {string} has no {string} link")]
        public void ThenTheRowHasNoLink(string code, string text)
            => Assert.DoesNotContain(text, Row(code).QuerySelectorAll("a, button, summary").Select(e => e.TextContent.Trim()));

        [Then("no evidence is stored")]
        public async Task ThenNoEvidenceIsStored()
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(0, await db.Evidence.CountAsync());
            Assert.Equal(0, await db.EvidenceDocuments.CountAsync());
        }

        [Then("the removed evidence {string} is still stored")]
        public async Task ThenTheRemovedEvidenceIsStillStored(string title)
        {
            await using var db = _web.CreateDbContext();
            var evidence = await db.Evidence.SingleAsync(e => e.Title == title);
            Assert.NotNull(evidence.RemovedAt);
            Assert.NotNull(evidence.RemovedById);
        }

        // ---------------------------------------------------------- Helpers

        // Opens the sub-tab with the requirement, fills in the row's "+ Evidence" form and sends it
        private async Task AddAsync(string code, string title, string? url = null, DateOnly? validUntil = null,
            string? fileName = null, byte[]? content = null)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            var form = Row(code).QuerySelector("form.add-evidence")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no \"+ Evidence\" form.");

            var fields = HiddenFields(form);
            fields["title"] = title;
            if (validUntil is { } until)
            {
                fields["validUntil"] = until.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (content is not null)
            {
                // PostFileAsync adds the antiforgery token itself; twice would be refused
                fields.Remove("__RequestVerificationToken");
                await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName!, content);
                if (!_web.Response!.IsSuccessStatusCode)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"The upload gave {(int)_web.Response.StatusCode}: {await _web.Response.Content.ReadAsStringAsync()}");
                }
            }
            else
            {
                fields["url"] = url ?? "";
                await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            }
        }

        private async Task<string> DownloadPathAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            var evidence = await db.Evidence.SingleAsync(e => e.Title == title);
            return $"/Projects/EvidenceFile/{evidence.ProjectId}?evidenceId={evidence.Id}";
        }

        private IElement Row(string code)
            => RequirementRowLookup.Find(_web, code)
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} lists no requirement '{code}'.");

        private IElement Evidence(string code, string title)
            => Row(code).QuerySelectorAll(".evidence").FirstOrDefault(e => e.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The row '{code}' shows no evidence '{title}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
