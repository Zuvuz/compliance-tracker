using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-054: the risk assessment's CSV, from the Reports tab's link (a#reports-risk-assessment-csv)
    [Binding]
    public class RiskAssessmentReportStepDefinitions
    {
        private readonly WebScenario _web;
        private List<string[]> _rows = [];

        public RiskAssessmentReportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I download the risk assessment of {string} as CSV")]
        public async Task WhenIDownloadAsCsv(string project)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}/reports");
            }

            var href = _web.Page!.QuerySelector("a#reports-risk-assessment-csv")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException("The Reports tab has no CSV of the risk assessment.");
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
            _rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(';')).ToList();
        }

        [Then("the risk assessment lists {string}, {string} and {string} in that order")]
        public void ThenListsInOrder(string first, string second, string third)
            => Assert.Equal([first, second, third], _rows.Select(r => r[0]));

        [Then("the risk assessment lists only {string}")]
        public void ThenListsOnly(string title) => Assert.Equal([title], _rows.Select(r => r[0]));

        [Then("the risk assessment shows {string} with the level {string}")]
        public void ThenShowsTheLevel(string title, string level) => Assert.Equal(level, Row(title)[1]);

        [Then("the risk assessment shows {string} with the measures {string}")]
        public void ThenShowsTheMeasures(string title, string measures) => Assert.Equal(measures, Row(title)[7]);

        [Then("the risk assessment shows {string} with the next review {string}")]
        public void ThenShowsTheNextReview(string title, string next) => Assert.Equal(next, Row(title)[10]);

        private string[] Row(string title)
            => _rows.FirstOrDefault(r => r[0] == title) ?? throw new Xunit.Sdk.XunitException($"The risk assessment has no '{title}'.");
    }
}
