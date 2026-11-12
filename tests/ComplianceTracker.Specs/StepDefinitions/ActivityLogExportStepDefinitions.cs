using System.Text;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-055: GET /Activity/Export, as the Activity page's CSV and PDF buttons (#export-activity-csv, #export-activity-pdf)
    [Binding]
    public class ActivityLogExportStepDefinitions
    {
        private readonly WebScenario _web;
        private byte[] _content = [];

        public ActivityLogExportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I export the activity log as {string}")]
        public async Task WhenIExport(string format)
        {
            // Through the button on the Activity page, so the page offers it
            await _web.OpenAsync("/Activity");
            var link = _web.Page!.QuerySelector($"#export-activity-{format}")
                ?? throw new Xunit.Sdk.XunitException($"The Activity page has no {format} export.");
            await GetAsync(_web.Client, link.GetAttribute("href")!);
        }

        [When("I export the activity log as {string} from {string} to {string}")]
        public Task WhenIExportAPeriod(string format, string from, string to)
            => GetAsync(_web.Client, $"/Activity/Export?format={format}&from={from}&to={to}");

        [When("{string} exports the activity log as {string}")]
        public async Task WhenExports(string userName, string format)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            await GetAsync(client, $"/Activity/Export?format={format}");
        }

        [Then("the export is a CSV file named {string}")]
        public void ThenTheExportIsACsvFile(string fileName)
        {
            Assert.Equal("text/csv", _web.Response!.Content.Headers.ContentType?.MediaType);
            Assert.Equal(fileName, _web.Response.Content.Headers.ContentDisposition?.FileNameStar ?? _web.Response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        }

        [Then("the export is a PDF file")]
        public void ThenTheExportIsAPdf()
            => Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(_content, 0, Math.Min(5, _content.Length)));

        [Then("the export has the columns {string}")]
        public void ThenTheExportHasTheColumns(string header) => Assert.Equal(header, Lines()[0]);

        [Then("the export has a row {string} for {string} by {string} as {string}")]
        public void ThenTheExportHasARow(string action, string project, string person, string role)
            => Assert.Contains(Lines().Skip(1).Select(l => l.Split(';')), cells => cells[1] == action && cells[2] == project && cells[4] == person && cells[5] == role);

        [Then("the export has no rows")]
        public void ThenTheExportHasNoRows() => Assert.Single(Lines());

        private async Task GetAsync(HttpClient client, string path)
        {
            _web.Response = await client.GetAsync(path);
            _content = _web.Response.IsSuccessStatusCode ? await _web.Response.Content.ReadAsByteArrayAsync() : [];
        }

        // The CSV's lines without the byte order mark
        private string[] Lines()
        {
            Assert.True(_web.Response!.IsSuccessStatusCode, $"The export gave {(int)_web.Response.StatusCode}.");
            return Encoding.UTF8.GetString(_content).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
