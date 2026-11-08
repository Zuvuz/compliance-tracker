using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-136: the organisation's documents, through the Documents page and the projects' Evidence tab.
    // UI contract: Views/Documents/Index.cshtml (see its header) and .evidence-item[data-title] with .from-organisation
    // and .evidence-requirement[data-code] on the Evidence tab.
    [Binding]
    public class OrganisationDocumentsStepDefinitions
    {
        private readonly WebScenario _web;
        private byte[]? _downloaded;

        public OrganisationDocumentsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I upload the policy {string} version {string} for the organisation")]
        public Task WhenIUploadThePolicy(string title, string version) => UploadAsync(title, version, $"{title} text", null);

        [Given("I have uploaded the policy {string} version {string} with the content {string}")]
        public Task GivenIHaveUploadedThePolicy(string title, string version, string content) => UploadAsync(title, version, content, null);

        [Given("I have uploaded the policy {string} version {string} due for review on {string}")]
        public Task GivenIHaveUploadedThePolicyDue(string title, string version, string reviewOn) => UploadAsync(title, version, $"{title} text", reviewOn);

        [Given("I link the document {string} to {string}")]
        [When("I link the document {string} to {string}")]
        public async Task WhenILinkTheDocument(string title, string code)
        {
            var form = await DocumentFormAsync(title, "form.link-document");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => (o.GetAttribute("value") ?? "").EndsWith("|" + code, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be chosen for '{title}'.");
            var fields = Hidden(form);
            fields["requirement"] = option.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I unlink the document {string} from {string}")]
        public async Task WhenIUnlinkTheDocument(string title, string code)
        {
            await OpenAsync();
            var form = Document(title).QuerySelectorAll(".document-link").FirstOrDefault(l => l.GetAttribute("data-code") == code)?.QuerySelector("form.unlink-document")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' is not linked to '{code}'.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I upload version {string} of {string} with the content {string}")]
        public async Task WhenIUploadVersion(string version, string title, string content)
        {
            var form = await DocumentFormAsync(title, "form.new-version");
            var fields = Hidden(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            fields["version"] = version;
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, "policy.pdf", Pdf(content));
            Assert.Empty(_web.Page!.QuerySelectorAll(".validation-summary-errors li").Select(e => e.TextContent.Trim()));
        }

        [When("I remove the document {string}")]
        public async Task WhenIRemoveTheDocument(string title)
        {
            var form = await DocumentFormAsync(title, "form.remove-document");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [When("I download the evidence {string} of {string}")]
        public async Task WhenIDownloadTheEvidence(string title, string project)
        {
            var item = await EvidenceItemAsync(project, title) ?? throw new Xunit.Sdk.XunitException($"'{project}' has no evidence '{title}'.");
            var href = item.QuerySelector("a[href*=\"EvidenceFile\"]")!.GetAttribute("href")!;
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            _downloaded = await response.Content.ReadAsByteArrayAsync();
        }

        // The page offers no remove button; the request is sent anyway, so the server must refuse it
        [When("I try to remove the evidence {string} from {string}")]
        public async Task WhenITryToRemoveTheEvidence(string title, string project)
        {
            var item = await EvidenceItemAsync(project, title) ?? throw new Xunit.Sdk.XunitException($"'{project}' has no evidence '{title}'.");
            Assert.Null(item.QuerySelector("form.remove-project-evidence"));
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            var evidenceId = await db.Evidence.Where(e => e.ProjectId == projectId && e.Title == title && e.RemovedAt == null).Select(e => e.Id).SingleAsync();
            await _web.PostFormAsync("/Projects/RemoveProjectEvidence", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["evidenceId"] = evidenceId.ToString()
            });
        }

        // ---------------------------------------------------------- Then

        [Then("{string} has the evidence {string} for {string} from the organisation")]
        public async Task ThenHasTheEvidenceFromTheOrganisation(string project, string title, string code)
        {
            var item = await EvidenceItemAsync(project, title) ?? throw new Xunit.Sdk.XunitException($"'{project}' has no evidence '{title}'.");
            Assert.NotNull(item.QuerySelector(".from-organisation"));
            Assert.Contains(item.QuerySelectorAll(".evidence-requirement"), r => r.GetAttribute("data-code") == code);
        }

        [Then("{string} has no evidence {string}")]
        public async Task ThenHasNoEvidence(string project, string title)
            => Assert.Null(await EvidenceItemAsync(project, title));

        [Then("the document {string} is used by {string}")]
        public async Task ThenTheDocumentIsUsedBy(string title, string projects)
        {
            await OpenAsync();
            Assert.Equal($"Used by {projects}", Document(title).QuerySelector(".used-by")!.TextContent.Trim());
        }

        [Then("the document {string} shows the earlier version {string}")]
        public async Task ThenTheDocumentShowsTheEarlierVersion(string title, string version)
        {
            await OpenAsync();
            Assert.Contains(version, Document(title).QuerySelector(".earlier-versions")?.TextContent ?? "", StringComparison.Ordinal);
        }

        [Then("the file has the content {string}")]
        public void ThenTheFileHasTheContent(string content)
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(_downloaded!);
            Assert.Contains(content, string.Concat(pdf.GetPages().Select(p => p.Text)), StringComparison.Ordinal);
        }

        [Then("the Documents page says {string}")]
        public async Task ThenTheDocumentsPageSays(string text)
        {
            await OpenAsync();
            Assert.Contains(text, _web.Page!.QuerySelector("#documents-due")?.TextContent ?? "", StringComparison.Ordinal);
        }

        [Then("\"Needs attention\" of {string} shows {string}")]
        public async Task ThenNeedsAttentionShows(string project, string text)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}");
            Assert.Contains(_web.Page!.QuerySelectorAll("#needs-attention li"), li => li.TextContent.Contains(text, StringComparison.Ordinal));
        }

        [Then("removing {string} asks to confirm that {string} loses it")]
        public async Task ThenRemovingAsksToConfirm(string title, string code)
        {
            var form = await DocumentFormAsync(title, "form.remove-document");
            Assert.Contains(code, form.GetAttribute("data-confirm") ?? "", StringComparison.Ordinal);
            Assert.Equal("true", form.GetAttribute("data-confirm-final"));
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string title, string version, string content, string? reviewOn)
        {
            await OpenAsync();
            var form = _web.Page!.QuerySelector("#upload-document");
            if (form is null)
            {
                // A role that may not upload sees no form (US-010 R5); the request is sent anyway. The form token comes
                // from a page the role may open.
                await _web.OpenAsync("/Projects");
            }

            await using var db = _web.CreateDbContext();
            var ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == "Anna").Select(u => u.Id).FirstAsync();
            var fields = new Dictionary<string, string>
            {
                ["kind"] = "Policy",
                ["title"] = title,
                ["version"] = version,
                ["ownerId"] = ownerId.ToString()
            };
            if (reviewOn is not null)
            {
                fields["reviewDueOn"] = reviewOn;
            }

            await _web.PostFileAsync("/Documents/Upload", fields, "policy.pdf", Pdf(content));
        }

        private async Task<IElement> DocumentFormAsync(string title, string selector)
        {
            await OpenAsync();
            return Document(title).QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"'{title}' has no form {selector}.");
        }

        private IElement Document(string title)
            => _web.Page!.QuerySelectorAll("li.organisation-document").FirstOrDefault(d => d.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Documents page has no '{title}'.");

        private Task OpenAsync() => _web.OpenAsync("/Documents");

        private async Task<IElement?> EvidenceItemAsync(string project, string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/evidence");
            return _web.Page!.QuerySelectorAll(".evidence-item").FirstOrDefault(e => e.GetAttribute("data-title") == title);
        }

        private static byte[] Pdf(string content) => TestFiles.PdfWithText(content);

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
