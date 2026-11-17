using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-171: Views/Shared/_Messages.cshtml (#messages with .message-success, role=status, data-fade) and the page's errors
    [Binding]
    public class MessagesStepDefinitions
    {
        private readonly WebScenario _web;

        public MessagesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the page shows the success {string} that fades")]
        public void ThenThePageShowsTheSuccess(string text)
        {
            var message = _web.Page!.QuerySelector("#messages .message-success") ?? throw new Xunit.Sdk.XunitException("No success message.");
            Assert.Equal(text, message.TextContent.Trim());
            Assert.Equal("status", message.GetAttribute("role"));
            Assert.Equal("6000", message.GetAttribute("data-fade"));
        }

        [Then("the error is an alert")]
        public void ThenTheErrorIsAnAlert()
        {
            var error = _web.Page!.QuerySelector(".validation-summary-errors") ?? throw new Xunit.Sdk.XunitException("No error.");
            Assert.Equal("alert", (error.Closest("[role]") ?? error).GetAttribute("role"));
        }
    }
}
