using System.IO.Compression;
using System.Net.Http.Headers;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-180: the public page "Verify an audit package" (Views/Verify/Index.cshtml: form#verify-package,
    // #verify-result[data-result], #verify-maker, #verify-differences li[data-kind][data-path]), used without signing in
    [Binding]
    public class VerifyAuditPackageStepDefinitions
    {
        private readonly WebScenario _web;
        private IDocument? _page;

        public VerifyAuditPackageStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("someone verifies the package without signing in")]
        public async Task WhenSomeoneVerifies() => await VerifyAsync(await PackageAsync());

        [When("someone verifies the package with {string} changed")]
        public async Task WhenSomeoneVerifiesChanged(string path)
            => await VerifyAsync(Rewrite(await PackageAsync(), files => files[path] = [.. files[path], 0x20]));

        [When("someone verifies the package with {string} removed and {string} added")]
        public async Task WhenSomeoneVerifiesRemovedAndAdded(string removed, string added)
            => await VerifyAsync(Rewrite(await PackageAsync(), files =>
            {
                files.Remove(removed);
                files[added] = "added later"u8.ToArray();
            }));

        [When("someone verifies a file that is not a ZIP")]
        public Task WhenSomeoneVerifiesNotAZip() => VerifyAsync("not a zip"u8.ToArray());

        [Then("the verify page says {string}")]
        public void ThenTheVerifyPageSays(string result)
            => Assert.Equal(result, Result().GetAttribute("data-result"));

        [Then("the verify page names {string} and {string}")]
        public void ThenTheVerifyPageNames(string organisation, string maker)
        {
            var text = _page!.QuerySelector("#verify-maker")?.TextContent ?? "(no maker)";
            Assert.Contains($"made by {organisation}", text, StringComparison.Ordinal);
            Assert.Contains($"by {maker}", text, StringComparison.Ordinal);
            Assert.Contains("Unchanged", Result().TextContent, StringComparison.Ordinal);
        }

        [Then("the verify page lists {string} {string}")]
        public void ThenTheVerifyPageLists(string kind, string path)
            => Assert.Contains(_page!.QuerySelectorAll("#verify-differences li"), li => li.GetAttribute("data-kind") == kind && li.GetAttribute("data-path") == path);

        private IElement Result()
            => _page!.QuerySelector("#verify-result")
               ?? throw new Xunit.Sdk.XunitException($"No result: {_page.QuerySelector("#verify-error")?.TextContent ?? _page.Body?.TextContent}");

        // Like a receiver: no sign-in, opens the page and uploads the file
        private async Task VerifyAsync(byte[] zip)
        {
            using var client = _web.CreateClient();
            var form = await WebScenario.ParseAsync(await client.GetAsync("/Verify"));
            var token = form.QuerySelector<IHtmlInputElement>("form#verify-package input[name='__RequestVerificationToken']")?.Value
                ?? throw new Xunit.Sdk.XunitException("The verify page has no form.");
            using var content = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" }
            };
            var file = new ByteArrayContent(zip);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(file, "file", "audit-package.zip");
            var response = await client.PostAsync(form.QuerySelector("form#verify-package")!.GetAttribute("action")!, content);
            response.EnsureSuccessStatusCode();
            _page = await WebScenario.ParseAsync(response);
        }

        // The newest package, downloaded like a locked report by its maker
        private async Task<byte[]> PackageAsync()
        {
            await using var db = _web.CreateDbContext();
            var snapshot = await db.ReportSnapshots.Where(s => s.Kind == "audit-package").OrderByDescending(s => s.LockedAt).FirstAsync();
            var response = await _web.Client.GetAsync($"/Projects/LockedReport/{snapshot.ProjectId}?snapshotId={snapshot.Id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private static byte[] Rewrite(byte[] zip, Action<Dictionary<string, byte[]>> change)
        {
            var files = new Dictionary<string, byte[]>();
            using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    using var stream = entry.Open();
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    files[entry.FullName] = memory.ToArray();
                }
            }

            change(files);
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (path, content) in files)
                {
                    using var stream = archive.CreateEntry(path).Open();
                    stream.Write(content);
                }
            }

            return output.ToArray();
        }
    }
}
