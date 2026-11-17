using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-175: #project-nav[data-project][data-current-tab][data-tabs]; [data-remember][data-remember-params] with a
    // button [data-reset-view] of the same key
    [Binding]
    public class RememberChoicesStepDefinitions
    {
        private readonly WebScenario _web;

        public RememberChoicesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the project's navigation says the current tab is {string} among its valid tabs")]
        public void ThenTheNavigationSaysTheTab(string tab)
        {
            var nav = _web.Page!.QuerySelector("#project-nav") ?? throw new Xunit.Sdk.XunitException("No project navigation.");
            Assert.False(string.IsNullOrEmpty(nav.GetAttribute("data-project")));
            Assert.Equal(tab, nav.GetAttribute("data-current-tab"));
            Assert.Contains(tab, (nav.GetAttribute("data-tabs") ?? "").Split(','));
        }

        [Then("the filters {string} are remembered with {string} and can be reset")]
        public void ThenTheFiltersAreRemembered(string id, string parameters)
        {
            var part = _web.Page!.QuerySelector($"#{id}[data-remember]") ?? throw new Xunit.Sdk.XunitException($"#{id} is not remembered.");
            Assert.Equal(parameters, part.GetAttribute("data-remember-params"));
            var key = part.GetAttribute("data-remember");
            Assert.Contains(_web.Page.QuerySelectorAll("button[data-reset-view]"), b => b.GetAttribute("data-reset-view") == key);
        }
    }
}
