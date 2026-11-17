using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-166: #shared-dialog (role=dialog, aria-labelledby, Cancel); [data-dialog-open] → [data-dialog][data-dialog-title];
    // <details data-dialog-title>
    [Binding]
    public class DialogsStepDefinitions
    {
        private readonly WebScenario _web;

        public DialogsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the page has the shared dialog for screen readers")]
        public void ThenThePageHasTheSharedDialog()
        {
            var dialog = _web.Page!.QuerySelector("#shared-dialog") ?? throw new Xunit.Sdk.XunitException("No shared dialog.");
            Assert.Equal("dialog", dialog.GetAttribute("role"));
            Assert.Equal("shared-dialog-title", dialog.GetAttribute("aria-labelledby"));
            Assert.NotNull(dialog.QuerySelector("#shared-dialog-title"));
            Assert.Contains(dialog.QuerySelectorAll(".modal-footer button"), b => b.TextContent.Trim() == "Cancel");
        }

        [Then("{string} opens the dialog {string}")]
        public void ThenTheButtonOpensTheDialog(string button, string title)
        {
            var opener = _web.Page!.QuerySelectorAll("[data-dialog-open]").FirstOrDefault(b => b.TextContent.Trim() == button)
                ?? throw new Xunit.Sdk.XunitException($"No \"{button}\" that opens a dialog.");
            var part = _web.Page.QuerySelector($"[data-dialog='{opener.GetAttribute("data-dialog-open")}']")
                ?? throw new Xunit.Sdk.XunitException($"\"{button}\" opens nothing.");
            Assert.Equal(title, part.GetAttribute("data-dialog-title"));
            Assert.NotNull(part.QuerySelector("form"));
        }

        [Then("the {string} form opens the dialog {string}")]
        public void ThenTheDetailsOpensTheDialog(string summary, string title)
        {
            var details = _web.Page!.QuerySelectorAll("details[data-dialog-title]").FirstOrDefault(d => d.QuerySelector("summary")?.TextContent.Trim() == summary)
                ?? throw new Xunit.Sdk.XunitException($"No \"{summary}\" that opens a dialog.");
            Assert.Equal(title, details.GetAttribute("data-dialog-title"));
            Assert.NotNull(details.QuerySelector("form"));
        }
    }
}
