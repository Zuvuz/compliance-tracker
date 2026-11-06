using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-052: the traceability matrix CSV, through the web app with test sign-in.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (the form #traceability-download with audience, format and sub).
    [Binding]
    public class TraceabilityMatrixStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private List<string[]> _lines = [];

        public TraceabilityMatrixStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // ---------------------------------------------------------- Given

        [Given("the link between {string} and {string} is removed")]
        public async Task GivenTheLinkIsRemoved(string story, string code)
        {
            await _compliance.OpenSubTabWithAsync("Acme Portal", code);
            await _compliance.WhenIRemoveTheLink(story, code);
        }

        // ---------------------------------------------------------- When

        // Chooses CSV and "All regulations" in the Compliance tab's download form, like a user
        [When("I download the traceability matrix of {string} as CSV")]
        public async Task WhenIDownloadTheMatrix(string projectName)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}/compliance");
            await DownloadAsync(_web.Client, FormUrl(format: "csv", audience: "team", sub: ""));
        }

        [When("I download the traceability matrix of the {string} sub-tab of {string}")]
        public async Task WhenIDownloadTheMatrixOfTheSubTab(string subTab, string projectName)
        {
            await _compliance.WhenIOpenTheSubTab(subTab, projectName);
            var sub = _web.Page!.QuerySelector("#traceability-download select[name=sub] option")!.GetAttribute("value")!;
            await DownloadAsync(_web.Client, FormUrl(format: "csv", audience: "team", sub: sub));
        }

        [When("{string} downloads the traceability matrix of {string} as CSV")]
        public async Task WhenDownloadsTheMatrix(string userName, string projectName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            await DownloadAsync(client, $"/Projects/Traceability/{await ProjectIdAsync(projectName)}?format=csv");
        }

        // ---------------------------------------------------------- Then

        [Then("the line for {string} has the status {string} and the story {string}")]
        public void ThenTheLineHasTheStory(string code, string status, string story)
            => Assert.Contains(LinesFor(code), l => l[Column("Status")] == status && l[Column("Story title")] == story);

        [Then("the line for {string} has the status {string} and no story")]
        public void ThenTheLineHasNoStory(string code, string status)
        {
            var line = Assert.Single(LinesFor(code));
            Assert.Equal(status, line[Column("Status")]);
            Assert.Equal("", line[Column("Story title")]);
        }

        [Then("the line for {string} says it was linked by {string} with the source {string}")]
        public void ThenTheLineSaysLinkedBy(string code, string userName, string source)
        {
            var line = Assert.Single(LinesFor(code));
            Assert.Equal(userName, line[Column("Linked by")]);
            Assert.Equal(source, line[Column("Link source")]);
            Assert.Matches(@"^\d{2}-\d{2}-\d{4}$", line[Column("Linked at")]);
        }

        [Then("the line for {string} has the status {string} and a reason")]
        public void ThenTheLineHasAReason(string code, string status)
        {
            var line = Assert.Single(LinesFor(code));
            Assert.Equal(status, line[Column("Status")]);
            Assert.NotEqual("", line[Column("Reason")]);
        }

        [Then("the line for {string} has the evidence {string}")]
        public void ThenTheLineHasTheEvidence(string code, string evidence)
            => Assert.Contains(evidence, Assert.Single(LinesFor(code))[Column("Evidence")]);

        [Then("every line is about {string}")]
        public void ThenEveryLineIsAbout(string framework)
        {
            Assert.NotEmpty(_lines.Skip(1));
            Assert.All(_lines.Skip(1), l => Assert.Equal(framework, l[Column("Regulation")]));
        }

        [Then("no cell starts with {string}")]
        public void ThenNoCellStartsWith(string start)
        {
            Assert.Contains(_lines, l => l.Any(c => c.Contains("HYPERLINK"))); // the story is in the file
            Assert.DoesNotContain(_lines.SelectMany(l => l), c => c.StartsWith(start, StringComparison.Ordinal));
        }

        [Then("the file has a header line")]
        public void ThenTheFileHasAHeaderLine()
            => Assert.Equal("Regulation", Assert.IsType<string[]>(_lines.FirstOrDefault())[0]);

        // ---------------------------------------------------------- Helpers

        // The form's address with the choices, as the browser sends a GET form
        private string FormUrl(string format, string audience, string sub)
        {
            var form = _web.Page!.QuerySelector("#traceability-download")
                ?? throw new Xunit.Sdk.XunitException("The Compliance tab has no download form.");
            return $"{form.GetAttribute("action")}?audience={audience}&format={format}&sub={Uri.EscapeDataString(sub)}";
        }

        // A CSV comes back when it works; otherwise the answer is kept for "the page is not found"
        private async Task DownloadAsync(HttpClient client, string path)
        {
            var response = await client.GetAsync(path);
            _web.Response = response;
            if (response.Content.Headers.ContentType?.MediaType != "text/csv")
            {
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()), "The CSV has no UTF-8 BOM, so Excel may show the wrong letters.");
            _lines = Parse(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }

        private int Column(string name) => Array.IndexOf(_lines[0], name) is var i and >= 0
            ? i
            : throw new Xunit.Sdk.XunitException($"The CSV has no column '{name}'.");

        private IReadOnlyList<string[]> LinesFor(string code)
            => _lines.Skip(1).Where(l => l[Column("Requirement code")] == code).ToList();

        // ";" separated, "" escapes a quote inside a quoted cell, lines end with \r\n
        private static List<string[]> Parse(string text)
        {
            var lines = new List<string[]>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') { quoted = false; }
                    else { cell.Append(c); }
                }
                else if (c == '"') { quoted = true; }
                else if (c == ';') { cells.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { cells.Add(cell.ToString()); cell.Clear(); lines.Add([.. cells]); cells.Clear(); }
                else { cell.Append(c); }
            }

            return lines;
        }

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
