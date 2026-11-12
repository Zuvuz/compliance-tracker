using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-053: the Statement of Applicability on the organisation's Reports tab (a#reports-soa, -csv, -pdf)
    [Binding]
    public class StatementOfApplicabilityStepDefinitions
    {
        private readonly WebScenario _web;
        private List<string[]> _rows = [];

        public StatementOfApplicabilityStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // The organisation area is made the first time /Organisation is opened
        public static async Task OpenOrganisationReportsAsync(WebScenario web)
        {
            await web.OpenAsync("/Organisation");
            await using var db = web.CreateDbContext();
            var area = await db.Projects.Where(p => p.Kind == Domain.Projects.ProjectKind.Organisation).Select(p => p.Id).SingleAsync();
            await web.OpenAsync($"/Projects/Details/{area}/reports");
        }

        [When("I open the organisation's Reports tab")]
        public Task WhenIOpenTheOrganisationsReportsTab() => OpenOrganisationReportsAsync(_web);

        [When("I download the Statement of Applicability as CSV")]
        public async Task WhenIDownloadAsCsv()
        {
            await OpenOrganisationReportsAsync(_web);
            var href = _web.Page!.QuerySelector("a#reports-soa-csv")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException("The organisation's Reports tab has no CSV of the Statement of Applicability.");
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
            _rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(';')).ToList();
        }

        [Then("the Statement of Applicability lists {string} and {string} in that order")]
        public void ThenListsInOrder(string first, string second) => Assert.Equal([first, second], _rows.Select(r => r[0]));

        [Then("the control {string} is {string} and {string}")]
        public void ThenTheControlIs(string code, string applicable, string implementation)
            => Assert.Equal((applicable, implementation), (Row(code)[2], Row(code)[4]));

        [Then("the control {string} has the reason {string}")]
        public void ThenTheControlHasTheReason(string code, string reason) => Assert.Equal(reason, Row(code)[3]);

        [Then("the control {string} is met by {string}")]
        public void ThenTheControlIsMetBy(string code, string howMet) => Assert.Equal(howMet, Row(code)[5]);

        private string[] Row(string code)
            => _rows.FirstOrDefault(r => r[0] == code) ?? throw new Xunit.Sdk.XunitException($"The Statement of Applicability has no '{code}'.");
    }
}
