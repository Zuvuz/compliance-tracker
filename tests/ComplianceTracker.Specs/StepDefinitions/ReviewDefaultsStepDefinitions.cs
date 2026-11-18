using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-195: the forms' dates, the warning #no-review-date, "To review" (.review-item[data-kind=WithoutDate]) and the
    // Settings form#set-review-defaults
    [Binding]
    public class ReviewDefaultsStepDefinitions
    {
        private readonly WebScenario _web;

        public ReviewDefaultsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I set the default evidence validity to {int} months")]
        public async Task WhenISetTheDefault(int months)
        {
            await _web.OpenAsync("/Settings");
            var form = _web.Page!.QuerySelector("form#set-review-defaults") ?? throw new Xunit.Sdk.XunitException("Settings has no review dates.");
            var fields = form.QuerySelectorAll("input[type=number]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["evidenceValidity"] = months.ToString();
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the field {string} holds {string}")]
        public void ThenTheFieldHolds(string id, string value)
            => Assert.Equal(value, _web.Page!.QuerySelector($"#{id}")?.GetAttribute("value"));

        [Then("I am warned {string}")]
        public void ThenIAmWarned(string text)
            => Assert.Equal(text, _web.Page!.QuerySelector("#no-review-date")?.TextContent.Trim());

        [Then("{string} lists {int} item(s) without a review date for {string}")]
        public async Task ThenToReviewLists(string page, int count, string project)
        {
            Assert.Equal("To review", page);
            await _web.OpenAsync("/Review");
            var item = _web.Page!.QuerySelector($".review-project[data-project='{project}'] .review-item[data-kind='WithoutDate']")
                ?? throw new Xunit.Sdk.XunitException($"To review lists nothing without a date for {project}.");
            Assert.StartsWith($"{count} ", item.TextContent.Trim());
        }
    }
}
