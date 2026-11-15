using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-170: the forms' data-busy text (shown by wwwroot/js/site.js while the form works)
    [Binding]
    public class AiProgressStepDefinitions
    {
        private readonly WebScenario _web;

        public AiProgressStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the form {string} says {string} while it works")]
        public void ThenTheFormSaysWhileItWorks(string selector, string text)
        {
            var form = _web.Page!.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"The page has no {selector}.");
            Assert.Equal(text, form.GetAttribute("data-busy"));
        }
    }
}
