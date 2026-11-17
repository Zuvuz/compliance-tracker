using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-172: <empty-state> (div.empty-state with .empty-state-title, .empty-state-why, .empty-state-actions and
    // .empty-state-cannot), keeping the list's id
    [Binding]
    public class EmptyPagesStepDefinitions
    {
        private readonly WebScenario _web;
        private IElement? _empty;

        public EmptyPagesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the empty list {string} says {string} and why it matters")]
        public void ThenTheEmptyListSays(string id, string title)
        {
            _empty = Empty(id);
            Assert.Equal(title, _empty.QuerySelector(".empty-state-title")?.TextContent.Trim());
            Assert.False(string.IsNullOrWhiteSpace(_empty.QuerySelector(".empty-state-why")?.TextContent), $"#{id} does not say why.");
        }

        [Then("it offers {string}, {string} and {string}")]
        public void ThenItOffers(string first, string second, string third)
        {
            var actions = _empty!.QuerySelectorAll(".empty-state-actions a, .empty-state-actions button").Select(a => a.TextContent.Trim()).ToList();
            Assert.Equal([first, second, third], actions);
        }

        [Then("the empty list {string} offers nothing and says {string}")]
        public void ThenOffersNothing(string id, string who)
        {
            var empty = Empty(id);
            Assert.Null(empty.QuerySelector(".empty-state-actions"));
            Assert.Contains(who, empty.QuerySelector(".empty-state-cannot")?.TextContent ?? "(nothing)", StringComparison.Ordinal);
        }

        private IElement Empty(string id)
            => _web.Page!.QuerySelector($"div.empty-state#{id}") ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no empty list #{id}.");
    }
}
