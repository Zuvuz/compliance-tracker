using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // #52 (small fixes): the activity log's "Hide" checkboxes (#activity-hide input[name=hide]) and the integrity check
    // moved from the organisation header (#organization-header) to the activity log (a#verify-log-integrity).
    [Binding]
    public class SmallFixesStepDefinitions
    {
        private readonly WebScenario _web;

        public SmallFixesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the organisation's activity log")]
        public Task WhenIOpenTheOrganisationsActivityLog() => _web.OpenAsync("/Activity");

        [When("I click \"Verify log integrity\"")]
        public async Task WhenIClickVerifyLogIntegrity()
        {
            var link = _web.Page!.QuerySelector("a#verify-log-integrity")
                ?? throw new Xunit.Sdk.XunitException("The activity log has no \"Verify log integrity\" link.");
            Assert.Equal("Verify log integrity", link.TextContent.Trim());
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the group {string} is ticked to be hidden")]
        public void ThenTheGroupIsTicked(string title)
        {
            var box = _web.Page!.QuerySelectorAll("#activity-hide .form-check")
                .FirstOrDefault(c => c.QuerySelector("label")?.TextContent.Trim() == title)?.QuerySelector("input[name=hide]")
                ?? throw new Xunit.Sdk.XunitException($"There is no \"Hide\" checkbox for '{title}'.");
            Assert.True(box.HasAttribute("checked"));
        }

        [Then("the page says the filter {string}")]
        public void ThenThePageSaysTheFilter(string message)
            => Assert.Contains(message, _web.Page!.QuerySelectorAll("#activity-ignored li").Select(l => l.TextContent.Trim()));

        [Then("the page explains what a fingerprint is")]
        public void ThenThePageExplainsFingerprints()
            => Assert.Contains("A fingerprint is", _web.Page!.QuerySelector("#fingerprint-explained")?.TextContent);

        [Then("the organisation header has no link to the check")]
        public async Task ThenTheHeaderHasNoLink()
        {
            await _web.OpenAsync("/Activity");
            Assert.Null(_web.Page!.QuerySelector("#check-activity-log"));
            Assert.DoesNotContain("Check the activity log", _web.Page!.Body!.TextContent);
        }
    }
}
