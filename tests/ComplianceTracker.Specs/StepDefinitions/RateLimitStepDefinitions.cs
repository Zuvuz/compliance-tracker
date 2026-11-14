using System.Net;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-097: many requests in a row, through the same forms a user sends, and the answers they got
    [Binding]
    public class RateLimitStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly List<HttpStatusCode> _answers = [];
        private string _lastBody = "";

        public RateLimitStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I ask the AI about {string} {int} times")]
        public async Task WhenIAskTheAiTimes(string code, int times)
        {
            var id = await ProjectIdAsync("Acme Portal");
            await _web.OpenAsync($"/Projects/Details/{id}/compliance/cra");
            var form = RequirementRowLookup.Find(_web, code)!.QuerySelector("form.ask-the-ai")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no \"Ask the AI\".");
            for (var i = 0; i < times; i++)
            {
                await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
                await RememberAsync();
            }
        }

        [When("I upload {int} scans to {string}")]
        public async Task WhenIUploadScans(int times, string project)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{id}/scans");
            var form = _web.Page!.QuerySelector("#upload-scan")!;
            var fields = Hidden(form);
            for (var i = 0; i < times; i++)
            {
                await _web.PostFileAsync(form.GetAttribute("action")!, fields, $"scan-{i}.sarif", TestScans.Findings("Trivy", 1, 0));
                await RememberAsync();
            }
        }

        [When("I open the projects page {int} times")]
        public async Task WhenIOpenTheProjectsPage(int times)
        {
            for (var i = 0; i < times; i++)
            {
                _web.Response = await _web.Client.GetAsync("/Projects");
                await RememberAsync();
            }
        }

        [Then("the first {int} answers were fine")]
        public void ThenTheFirstWereFine(int count)
            => Assert.All(_answers.Take(count), status => Assert.Equal(HttpStatusCode.OK, status));

        [Then("every answer was fine")]
        public void ThenEveryAnswerWasFine() => Assert.All(_answers, status => Assert.Equal(HttpStatusCode.OK, status));

        [Then("the last answer says {string}")]
        public void ThenTheLastAnswerSays(string text)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, _answers[^1]);
            Assert.Contains(text, _lastBody, StringComparison.Ordinal);
        }

        [Then("{string} has {int} scans")]
        public async Task ThenHasScans(string project, int count)
        {
            await using var db = _web.CreateDbContext();
            var id = await ProjectIdAsync(project);
            Assert.Equal(count, await db.Scans.CountAsync(s => s.ProjectId == id));
        }

        private async Task RememberAsync()
        {
            _answers.Add(_web.Response!.StatusCode);
            _lastBody = await _web.Response.Content.ReadAsStringAsync();
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        // PostFileAsync and PostFormAsync add the form token themselves
        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
