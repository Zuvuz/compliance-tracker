using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-123: the training data export on System → AI quality. UI contract: Views/System/AiQuality.cshtml
    // (form #export-training-data with from, to and the checkboxes kinds).
    [Binding]
    public class TrainingDataExportStepDefinitions
    {
        private const string Page = "/System/AiQuality";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private List<JsonElement> _lines = new();

        public TrainingDataExportStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        [When("I export the training data")]
        public Task WhenIExport() => ExportAsync(null, null, ["story", "evidence", "not-applicable"]);

        [When("I export the training data until yesterday")]
        public Task WhenIExportUntilYesterday()
            => ExportAsync(null, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime).AddDays(-1), ["story", "evidence", "not-applicable"]);

        [When("I export the training data of the kind {string}")]
        public Task WhenIExportTheKind(string kind) => ExportAsync(null, null, [kind]);

        // R1: an organisation admin, past the page
        [When("I post the training data export directly")]
        public async Task WhenIPostDirectly()
        {
            using var client = _web.CreateClient(signedInAs: "Anna", followRedirects: false);
            _web.Response = await client.PostAsync("/System/ExportTrainingData", new FormUrlEncodedContent([new("kinds", "story")]));
        }

        [Then("the file has {int} line")]
        [Then("the file has {int} lines")]
        public void ThenTheFileHas(int count) => Assert.Equal(count, _lines.Count);

        [Then("the line is a {string} decision {string} from {string} and {string}")]
        public void ThenTheLineIs(string kind, string decision, string organisation, string project)
        {
            var line = Assert.Single(_lines);
            Assert.Equal(kind, line.GetProperty("kind").GetString());
            Assert.Equal(decision, line.GetProperty("decision").GetString());
            Assert.Equal(organisation, line.GetProperty("organisation").GetString());
            Assert.Equal(project, line.GetProperty("project").GetString());
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", line.GetProperty("decidedAt").GetString());
        }

        [Then("the line has the story {string} and the model {string}")]
        public void ThenTheLineHasTheStory(string story, string model)
        {
            var line = Assert.Single(_lines);
            Assert.StartsWith(story, line.GetProperty("story").GetString());
            Assert.Contains("SP-1", line.GetProperty("requirement").GetString());
            Assert.Equal(model, line.GetProperty("embeddingModel").GetString());
            Assert.True(line.GetProperty("score").GetDouble() > 0);
        }

        // R4: no person's name, e-mail or id
        [Then("no line contains {string} or an e-mail")]
        public async Task ThenNoNames(string name)
        {
            var text = string.Join('\n', _lines.Select(l => l.GetRawText()));
            Assert.DoesNotContain(name, text);
            Assert.DoesNotMatch(new Regex(@"[\w.+-]+@[\w-]+\.[\w.]+"), text);
            await using var db = _web.CreateDbContext();
            foreach (var id in (await db.Organizations.ToListAsync()).SelectMany(o => o.Users).Select(u => u.Id))
            {
                Assert.DoesNotContain(id.ToString(), text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Then("the log of {string} says the system owner exported {int} decision")]
        public async Task ThenTheLogSays(string organisation, int count)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Organizations.Where(o => o.Name == organisation).Select(o => o.Id).SingleAsync();
            var entry = await db.ActivityLog.SingleAsync(e => e.OrganizationId == id && e.Action == ActivityAction.TrainingDataExported);
            Assert.Null(entry.UserId);
            Assert.StartsWith($"The system owner exported {count} decisions", entry.Details);
        }

        // Like the browser: the form on the page, with its antiforgery token and the checked kinds
        private async Task ExportAsync(DateOnly? from, DateOnly? to, string[] kinds)
        {
            await _web.OpenAsync(Page);
            var form = _web.Page!.QuerySelector("#export-training-data")
                ?? throw new Xunit.Sdk.XunitException("The AI quality page has no training data export.");
            var fields = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", _web.AntiforgeryToken()) };
            if (from is { } f)
            {
                fields.Add(new("from", f.ToString("yyyy-MM-dd")));
            }

            if (to is { } t)
            {
                fields.Add(new("to", t.ToString("yyyy-MM-dd")));
            }

            fields.AddRange(kinds.Select(k => new KeyValuePair<string, string>("kinds", k)));
            var response = await _web.Client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
            Assert.EndsWith(".jsonl", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
            var text = await response.Content.ReadAsStringAsync();
            _lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
        }
    }
}
