using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-026: the Overview tab's #classification card, with a .classification[data-framework] per regulation holding
    // .classification-class, .classification-guide, .classification-history li and the form .classify-form
    [Binding]
    public class ClassificationStepDefinitions
    {
        private readonly WebScenario _web;

        public ClassificationStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("{string} is classified under {string} as {string}")]
        public Task GivenIsClassified(string project, string framework, string classTitle)
            => ClassifyAsync(project, framework, classTitle, "The first assessment");

        [Given("I classify {string} under {string} as {string} because {string}")]
        [When("I classify {string} under {string} as {string} because {string}")]
        public Task WhenIClassify(string project, string framework, string classTitle, string reason)
            => ClassifyAsync(project, framework, classTitle, reason);

        [When("I classify {string} under {string} as {string} without a reason")]
        public Task WhenIClassifyWithoutReason(string project, string framework, string classTitle)
            => ClassifyAsync(project, framework, classTitle, "");

        [Then("{string} shows {string} as {string}")]
        public async Task ThenShows(string project, string framework, string classTitle)
        {
            await OpenAsync(project);
            Assert.Equal(classTitle, Regulation(framework).QuerySelector(".classification-class")!.TextContent.Trim());
        }

        [Then("the classes for {string} are {string}, {string}, {string}, {string} and {string}")]
        public void ThenTheClassesAre(string framework, string a, string b, string c, string d, string e)
            => Assert.Equal(new[] { a, b, c, d, e },
                Regulation(framework).QuerySelectorAll("select[name=classCode] option").Select(o => o.TextContent.Trim()));

        // #52: the browser stops the form while the reason is empty (the server still checks it)
        [Then("the classify form for {string} needs a reason")]
        public void ThenTheClassifyFormNeedsAReason(string framework)
            => Assert.True(Regulation(framework).QuerySelector("form.classify-form textarea[name=reason]")!.HasAttribute("required"));

        [Then("the guide for {string} names {string}")]
        public void ThenTheGuideNames(string framework, string text)
            => Assert.Contains(text, Regulation(framework).QuerySelector(".classification-guide")!.TextContent);

        [Then("the classification history of {string} under {string} shows {string} followed by {string}")]
        public async Task ThenTheHistoryShows(string project, string framework, string first, string second)
        {
            await OpenAsync(project);
            var history = Regulation(framework).QuerySelectorAll(".classification-history li")
                .Select(li => li.TextContent.Split(" – ")[0].Trim())
                .ToList();
            Assert.Equal(new[] { first, second }, history);
        }

        // ---------------------------------------------------------- Helpers

        private async Task ClassifyAsync(string project, string framework, string classTitle, string reason)
        {
            await OpenAsync(project);
            var form = Regulation(framework).QuerySelector("form.classify-form")
                ?? throw new Xunit.Sdk.XunitException($"'{framework}' cannot be classified on the page.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["classCode"] = form.QuerySelectorAll("select[name=classCode] option")
                .Single(o => o.TextContent.Trim() == classTitle).GetAttribute("value")!;
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task OpenAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/overview");
        }

        private IElement Regulation(string framework)
            => _web.Page!.QuerySelectorAll("#classification .classification").FirstOrDefault(c => c.GetAttribute("data-framework") == framework)
               ?? throw new Xunit.Sdk.XunitException($"The Overview shows no classification for '{framework}'.");
    }
}
