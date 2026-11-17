using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-174: the message after a small removal (#messages .message-undo with form.undo-removal)
    [Binding]
    public class UndoRemovalsStepDefinitions
    {
        private readonly WebScenario _web;

        public UndoRemovalsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the message says {string} and offers Undo")]
        public void ThenTheMessageOffersUndo(string text)
        {
            var message = Undo() ?? throw new Xunit.Sdk.XunitException("No Undo is offered.");
            Assert.Equal(text, message.QuerySelector("span")?.TextContent.Trim());
            Assert.Equal("Undo", message.QuerySelector("form.undo-removal button")?.TextContent.Trim());
            Assert.Equal("status", message.GetAttribute("role"));
        }

        [When("I press Undo")]
        public async Task WhenIPressUndo()
        {
            var form = Undo()?.QuerySelector("form.undo-removal") ?? throw new Xunit.Sdk.XunitException("No Undo is offered.");
            await _web.PostFormAsync(form.GetAttribute("action")!, form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? ""));
        }

        [Then("no Undo is offered")]
        public void ThenNoUndo() => Assert.Null(Undo());

        [Then("the risk {string} has the measure {string} again")]
        public async Task ThenTheRiskHasTheMeasureAgain(string title, string code)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Details/{id}");
            Assert.Contains(_web.Page!.QuerySelectorAll("#risk-measures li.risk-measure"), li => li.TextContent.Trim().StartsWith(code + " ", StringComparison.Ordinal));
        }

        [Then("the form {string} asks nothing first")]
        public void ThenTheFormAsksNothing(string selector)
        {
            var form = _web.Page!.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"No {selector}.");
            Assert.Null(form.GetAttribute("data-confirm"));
        }

        private IElement? Undo() => _web.Page!.QuerySelector("#messages .message-undo");
    }
}
