using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-200: following one requirement to the end. UI contract: Views/Projects/_RequirementPage.cshtml
    // (section#requirement-path with #path-progress, #requirement-complete, #next-requirement[data-ready] and
    // li.path-step[data-done] with .path-step-text and a.path-step-action[data-next] for the next open step).
    [Binding]
    public class FollowRequirementStepDefinitions
    {
        private readonly WebScenario _web;

        public FollowRequirementStepDefinitions(WebScenario web) => _web = web;

        private IElement Path => _web.Page!.QuerySelector("#requirement-path")
            ?? throw new Xunit.Sdk.XunitException("The requirement's page has no steps.");

        private List<IElement> Steps => Path.QuerySelectorAll("li.path-step").ToList();

        private static string Text(IElement step) => step.QuerySelector(".path-step-text")!.TextContent.Trim();

        [Then("the steps of {string} and {string} are listed")]
        public void ThenTheStepsOfAreListed(string first, string second)
        {
            var texts = Steps.Select(Text).ToList();
            foreach (var story in new[] { first, second })
            {
                Assert.Contains($"{story}: done", texts);
                Assert.Contains($"{story}: evidence", texts);
            }
            // R1: the stories come before the requirement's own steps
            Assert.True(texts.IndexOf($"{second}: evidence") < texts.IndexOf("Evidence on the requirement"));
        }

        [Then("the progress says {string}")]
        public void ThenThePageSays(string progress)
            => Assert.Equal(progress, Path.QuerySelector("#path-progress")!.TextContent.Trim());

        [Then("the next step is {string} with the action {string}")]
        public void ThenTheNextStepIs(string text, string action)
        {
            // R4: exactly one next step, and it is the first open one the member can do in the tracker
            var next = Assert.Single(Path.QuerySelectorAll("a.path-step-action[data-next='true']"));
            Assert.Equal(action, next.TextContent.Trim());
            Assert.Equal(text, Text(next.Closest("li.path-step")!));
            Assert.False(next.GetAttribute("href")!.StartsWith("http"), "The next step leaves the tracker.");
            // US-153: the header's "Add evidence" stays the one primary button
            Assert.Empty(Path.QuerySelectorAll(".btn-primary"));
            Assert.Contains("btn-primary", _web.Page!.QuerySelector("#primary-action")!.ClassName);
        }

        [Then("{string} is there but does not stand out")]
        public void ThenNextRequirementDoesNotStandOut(string label)
        {
            var next = Path.QuerySelector("#next-requirement") ?? throw new Xunit.Sdk.XunitException($"No '{label}'.");
            Assert.StartsWith(label, next.TextContent.Trim());
            Assert.Equal("false", next.GetAttribute("data-ready"));
            Assert.Null(Path.QuerySelector("#requirement-complete"));
        }

        [Then("the step {string} is done")]
        public void ThenTheStepIsDone(string text)
        {
            var step = Steps.SingleOrDefault(s => Text(s) == text) ?? throw new Xunit.Sdk.XunitException($"No step '{text}'.");
            Assert.Equal("true", step.GetAttribute("data-done"));
            Assert.Null(step.QuerySelector("a.path-step-action"));
        }

        [Then("the page has no steps")]
        public void ThenThePageHasNoSteps()
            => Assert.Null(_web.Page!.QuerySelector("#requirement-path"));
    }
}
