using System.IO.Compression;
using System.Text;

using ComplianceTracker.Specs.Support;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-188: the NIS2 gap analysis page (/GapAnalysis): form#gap-upload, #gap-upload-result rows with data-file and
    // data-outcome, #gap-suggestions rows with data-requirement and data-document, .gap-quote and form.approve-gap-suggestion.
    [Binding]
    public class GapAnalysisStepDefinitions
    {
        private const string Path = "/GapAnalysis";
        private readonly WebScenario _web;

        public GapAnalysisStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I upload to the NIS2 gap analysis the ZIP {string} with {string} saying {string} and {string}")]
        public Task WhenIUploadAZip(string zipName, string textFile, string text, string otherFile)
        {
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                Write(zip, "documents/" + textFile, Encoding.UTF8.GetBytes(Unescape(text)));
                Write(zip, "documents/" + otherFile, [0x00, 0x01, 0x02, 0x03]);
                Write(zip, "__MACOSX/._" + textFile, [0x00]); // left out, like the system's own files
            }

            return UploadAsync((zipName, memory.ToArray()));
        }

        [Given("I have uploaded to the NIS2 gap analysis {string} saying {string}")]
        [When("I upload to the NIS2 gap analysis {string} saying {string}")]
        public Task WhenIUploadAFile(string fileName, string text)
            => UploadAsync((fileName, Encoding.UTF8.GetBytes(Unescape(text))));

        [When("I upload to the NIS2 gap analysis the Word document {string} saying {string}")]
        public Task WhenIUploadAWordDocument(string fileName, string text)
        {
            using var memory = new MemoryStream();
            using (var document = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            }

            return UploadAsync((fileName, memory.ToArray()));
        }

        [Given("I have approved the gap suggestion {string} for {string}")]
        [When("I approve the gap suggestion {string} for {string}")]
        public async Task WhenIApprove(string code, string document)
        {
            await _web.OpenAsync(Path);
            var form = Row(code, document).QuerySelector("form.approve-gap-suggestion")!;
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Equal(Path, _web.CurrentPath); // back on the gap analysis
        }

        [When("I open the NIS2 gap analysis")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        // US-189
        [Given("I have answered {string} with {string} and {string}")]
        [When("I answer {string} with {string} and {string}")]
        public async Task WhenIAnswer(string code, string maturity, string comment)
        {
            await _web.OpenAsync(Path);
            var form = Question(code).QuerySelector("form.answer-question")
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be answered.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["maturity"] = maturity;
            fields["comment"] = comment;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the question {string} is covered by evidence")]
        public void ThenCovered(string code) => Assert.Equal("true", Question(code).GetAttribute("data-covered"));

        [Then("the question {string} is open")]
        public void ThenOpen(string code) => Assert.Equal("true", Question(code).GetAttribute("data-open"));

        [Then("the question {string} has the answer {string}")]
        public void ThenHasTheAnswer(string code, string answer)
            => Assert.Contains(answer, Question(code).QuerySelector(".gap-answer")?.TextContent ?? "(no answer)");

        [Then("the question {string} needs evidence")]
        public void ThenNeedsEvidence(string code) => Assert.Equal("true", Question(code).GetAttribute("data-needs-evidence"));

        [Then("the question {string} lists {int} earlier answer(s)")]
        public void ThenListsEarlier(string code, int count)
            => Assert.Equal(count, Question(code).QuerySelectorAll(".gap-earlier li").Length);

        [Then("the question {string} cannot be answered")]
        public void ThenCannotBeAnswered(string code) => Assert.Null(Question(code).QuerySelector("form.answer-question"));

        // US-190
        [Given("I have made the NIS2 gap report")]
        [When("I make the NIS2 gap report")]
        public async Task WhenIMakeTheReport()
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync(_web.Page!.QuerySelector("form#make-gap-report")!.GetAttribute("action")!, new Dictionary<string, string>());
        }

        [When("I make the gap {string} a risk")]
        public async Task WhenIMakeTheGapARisk(string code)
        {
            var link = _web.Page!.QuerySelector($"#gap-actions tr[data-code='{code}'] a.gap-make-risk")
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be made a risk.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the measure {string} is {string}")]
        public void ThenTheMeasureIs(string code, string status)
            => Assert.Equal(status, StatusRow(code).QuerySelector(".gap-status")?.TextContent.Trim());

        [Then("the measure {string} changed {string} since the last report")]
        public async Task ThenTheMeasureChanged(string code, string change)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(change, StatusRow(code).QuerySelector(".gap-change")?.TextContent.Trim());
        }

        [Then("the action plan lists {string} with the priority {string}")]
        public void ThenTheActionPlanLists(string code, string priority)
            => Assert.Equal(priority, _web.Page!.QuerySelector($"#gap-actions tr[data-code='{code}'] .gap-priority")?.TextContent.Trim());

        [Then("the action plan does not list {string}")]
        public void ThenTheActionPlanDoesNotList(string code)
            => Assert.Null(_web.Page!.QuerySelector($"#gap-actions tr[data-code='{code}']"));

        [Then("the gap report is listed with a fingerprint")]
        public async Task ThenTheReportIsListed()
        {
            await using var db = _web.CreateDbContext();
            var sha = await db.ReportSnapshots.Where(r => r.Kind == "nis2-gap").Select(r => r.Sha256).SingleAsync();
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector($"#gap-reports tr[data-sha256='{sha}']"));
        }

        [Then("the gap report's PDF says {string}")]
        public async Task ThenThePdfSays(string text)
        {
            await _web.OpenAsync(Path);
            var href = _web.Page!.QuerySelector("#gap-reports a")!.GetAttribute("href")!;
            var bytes = await (await _web.Client.GetAsync(href)).Content.ReadAsByteArrayAsync();
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            var all = string.Join(" ", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
            Assert.Contains(text, all);
        }

        [Then("the risk form is filled in with {string}")]
        public void ThenTheRiskFormIsFilledIn(string title)
            => Assert.StartsWith(title, _web.Page!.QuerySelector("input[name='Title']")?.GetAttribute("value") ?? "");

        // ---------------------------------------------------------- Then

        [Then("the gap upload says {string}")]
        public void ThenTheUploadSays(string summary)
            => Assert.Equal(summary, _web.Page!.QuerySelector("#gap-upload-summary")?.TextContent.Trim());

        [Then("the gap upload lists {string} as {string} saying {string}")]
        public void ThenTheUploadLists(string fileName, string outcome, string note)
        {
            var row = _web.Page!.QuerySelector($"#gap-upload-result tr[data-file='{fileName}']")
                ?? throw new Xunit.Sdk.XunitException($"The upload lists no '{fileName}': {_web.Page.QuerySelector("#gap-upload-result")?.TextContent}");
            Assert.Equal(outcome, row.GetAttribute("data-outcome"));
            Assert.Contains(Unescape(note), row.TextContent);
        }

        [Then("the gap analysis suggests {string} for {string} quoting {string}")]
        public async Task ThenItSuggests(string code, string document, string quote)
        {
            await _web.OpenAsync(Path);
            Assert.Contains(quote, Row(code, document).QuerySelector(".gap-quote")?.TextContent ?? "(no quote)");
        }

        [Then("the gap analysis has no suggestions")]
        public async Task ThenNoSuggestions()
        {
            await _web.OpenAsync(Path);
            Assert.NotNull(_web.Page!.QuerySelector("#gap-no-suggestions"));
        }

        [Then("the gap analysis has no upload form")]
        public void ThenNoUploadForm() => Assert.Null(_web.Page!.QuerySelector("form#gap-upload"));

        [Then("the organisation has {int} document(s)")]
        public async Task ThenTheOrganisationHasDocuments(int count)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(count, await db.Set<Domain.Organizations.OrganisationDocument>().CountAsync(d => d.RemovedAt == null));
        }

        // ---------------------------------------------------------- Helpers

        private AngleSharp.Dom.IElement StatusRow(string code)
            => _web.Page!.QuerySelector($"#gap-status tr[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"No measure '{code}' on {_web.CurrentPath}.");

        private AngleSharp.Dom.IElement Question(string code)
            => _web.Page!.QuerySelector($".gap-question[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"No question '{code}' on {_web.CurrentPath}.");

        private AngleSharp.Dom.IElement Row(string code, string document)
            => _web.Page!.QuerySelector($"#gap-suggestions tr[data-requirement='{code}'][data-document='{document}']")
               ?? throw new Xunit.Sdk.XunitException($"No suggestion '{code}' for '{document}': {_web.Page.QuerySelector("main")?.TextContent}");

        private async Task UploadAsync(params (string Name, byte[] Content)[] files)
        {
            await _web.OpenAsync(Path);
            using var form = new MultipartFormDataContent { { new StringContent(_web.AntiforgeryToken()), "__RequestVerificationToken" } };
            foreach (var (name, content) in files)
            {
                form.Add(new ByteArrayContent(content), "files", name);
            }

            _web.Response = await _web.Client.PostAsync(_web.Page!.QuerySelector("form#gap-upload")!.GetAttribute("action")!, form);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        private static void Write(ZipArchive zip, string name, byte[] content)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(content);
        }

        private static string Unescape(string text) => text.Replace("\\n", "\n").Replace("\\\"", "\"");
    }
}
