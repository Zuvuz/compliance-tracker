using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-169: Views/Review/Index.cshtml (#review-inbox, .review-project[data-project] with .review-item and a.review-open,
    // #review-empty) and the top bar (#top-bar > li).
    [Binding]
    public class ToReviewStepDefinitions
    {
        private readonly WebScenario _web;
        private string? _open;

        public ToReviewStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the inbox \"To review\"")]
        public async Task WhenIOpenToReview()
        {
            await _web.OpenAsync("/Projects");
            var link = _web.Page!.QuerySelector("#top-bar a#nav-review") ?? throw new Xunit.Sdk.XunitException("The top bar has no \"To review\".");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("\"To review\" lists {string} for {string}")]
        public void ThenItLists(string text, string project)
        {
            var section = _web.Page!.QuerySelectorAll(".review-project").Single(p => p.GetAttribute("data-project") == project);
            var item = section.QuerySelectorAll(".review-item").Single(i => i.TextContent.Contains(text, StringComparison.Ordinal));
            _open = item.QuerySelector("a.review-open")!.GetAttribute("href");
        }

        [Then("it opens the {string} tab of {string}")]
        public async Task ThenItOpens(string group, string project)
        {
            await _web.OpenAsync(_open!);
            Assert.Equal(group, _web.Page!.QuerySelector("#project-groups a.active")?.GetAttribute("data-group"));
        }

        [Then("\"To review\" says that nothing waits")]
        public void ThenNothingWaits() => Assert.NotNull(_web.Page!.QuerySelector("#review-empty"));

        [Then("the top bar has at most {int} items")]
        public void ThenAtMost(int count)
            => Assert.True(_web.Page!.QuerySelectorAll("#top-bar > li").Length <= count, "The top bar has too many items.");
    }
}
