using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-109: the audit page (/Projects/DoneAudit/{id}): .done-row[data-title][data-meets] with .done-criterion[data-criterion]
    // [data-state] holding .done-note and form.tick-criterion, form.mark-done, #done-csv, and form.change-definition-of-done
    [Binding]
    public class DefinitionOfDoneStepDefinitions
    {
        private readonly WebScenario _web;
        private string _csv = "";

        public DefinitionOfDoneStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the Definition of Done audit of {string}")]
        public async Task WhenIOpenTheAudit(string project)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/product-requirements");
            await _web.OpenAsync(_web.Page!.QuerySelector("#done-audit-link")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No link to the Definition of Done."));
        }

        [When("I tick {string} on {string} in {string}")]
        public async Task WhenITick(string criterion, string item, string project)
        {
            await WhenIOpenTheAudit(project);
            await PostAsync(Criterion(item, criterion).QuerySelector("form.tick-criterion") ?? throw new Xunit.Sdk.XunitException($"'{criterion}' cannot be ticked."));
            await WhenIOpenTheAudit(project);
        }

        [When("I mark {string} of {string} as Done")]
        public async Task WhenIMarkDone(string item, string project)
        {
            await WhenIOpenTheAudit(project);
            await PostAsync(Row(item).QuerySelector("form.mark-done") ?? throw new Xunit.Sdk.XunitException($"'{item}' cannot be marked Done."));
            await WhenIOpenTheAudit(project);
        }

        [When("I export the Definition of Done audit of {string} as CSV")]
        public async Task WhenIExport(string project)
        {
            await WhenIOpenTheAudit(project);
            var href = _web.Page!.QuerySelector("#done-csv")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No CSV export.");
            var response = await _web.Client.GetAsync(href);
            Assert.True(response.IsSuccessStatusCode, $"The export answered {(int)response.StatusCode}");
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            _csv = await response.Content.ReadAsStringAsync();
        }

        [When("I change the user stories' manual criteria of {string} to {string} and {string}")]
        public async Task WhenIChange(string project, string first, string second)
        {
            await WhenIOpenTheAudit(project);
            var form = _web.Page!.QuerySelector("form.change-definition-of-done[data-kind='UserStory']") ?? throw new Xunit.Sdk.XunitException("The criteria cannot be changed.");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .Select(i => new KeyValuePair<string, string>(i.GetAttribute("name")!, i.GetAttribute("value") ?? ""))
                .Concat(form.QuerySelectorAll("input[name=checks][checked]").Select(i => new KeyValuePair<string, string>("checks", i.GetAttribute("value")!)))
                .Append(new KeyValuePair<string, string>("manual", $"{first}\n{second}"))
                .ToList();
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        [Then("{string} does not meet the Definition of Done")]
        public void ThenDoesNotMeet(string item)
        {
            var row = Row(item);
            Assert.Equal(("true", "false"), (row.GetAttribute("data-finished"), row.GetAttribute("data-meets")));
        }

        [Then("{string} misses {string} because {string}")]
        public void ThenMisses(string item, string criterion, string why)
        {
            var status = Criterion(item, criterion);
            Assert.Equal("NotMet", status.GetAttribute("data-state"));
            Assert.Equal($"· {why}", status.QuerySelector(".done-note")?.TextContent.Trim());
        }

        [Then("{string} meets {string}")]
        public void ThenMeets(string item, string criterion) => Assert.Equal("Met", Criterion(item, criterion).GetAttribute("data-state"));

        [Then("{string} meets {string} with a note starting {string}")]
        public void ThenMeetsWithNote(string item, string criterion, string note)
        {
            var status = Criterion(item, criterion);
            Assert.Equal("Met", status.GetAttribute("data-state"));
            Assert.StartsWith($"· {note}", status.QuerySelector(".done-note")?.TextContent.Trim());
        }

        [Then("the export has a line for {string} and {string} with the status {string}")]
        public void ThenTheExportHas(string item, string criterion, string status)
        {
            var lines = _csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("﻿Work item,Kind,Finished on", lines[0].TrimStart());
            Assert.Contains(lines, l => l.Contains($"\"{item}\"") && l.Contains($"\"{criterion}\",\"{status}\""));
        }

        [Then("the user stories' criteria of {string} end with {string}")]
        public async Task ThenTheCriteriaEndWith(string project, string text)
        {
            await WhenIOpenTheAudit(project);
            Assert.EndsWith(text, _web.Page!.QuerySelector("section[data-kind='UserStory'] .definition-text")?.TextContent.Trim());
        }

        private AngleSharp.Dom.IElement Row(string item)
            => _web.Page!.QuerySelectorAll(".done-row").FirstOrDefault(r => r.GetAttribute("data-title") == item)
               ?? throw new Xunit.Sdk.XunitException($"The audit has no '{item}'.");

        private AngleSharp.Dom.IElement Criterion(string item, string criterion)
            => Row(item).QuerySelectorAll(".done-criterion").FirstOrDefault(c => c.GetAttribute("data-criterion") == criterion)
               ?? throw new Xunit.Sdk.XunitException($"'{item}' has no criterion '{criterion}'.");

        private async Task PostAsync(AngleSharp.Dom.IElement form)
        {
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
