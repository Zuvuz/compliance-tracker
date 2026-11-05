using Reqnroll;
using Xunit.Abstractions;

namespace ComplianceTracker.Specs.Support
{
    // US-058 (B45): xUnit puts no Reqnroll tags in a TRX file, so every scenario writes its tags to its output, where the
    // tracker reads them when the test results are uploaded ("Requirement tags: @cra-I.2(d) @iso27001-A.5.15").
    [Binding]
    public sealed class RequirementTagsHook
    {
        private readonly ScenarioContext _scenario;
        private readonly ITestOutputHelper _output;

        public RequirementTagsHook(ScenarioContext scenario, ITestOutputHelper output)
        {
            _scenario = scenario;
            _output = output;
        }

        [AfterScenario]
        public void WriteTags()
        {
            var tags = _scenario.ScenarioInfo.CombinedTags.Where(t => t.Contains('-')).Select(t => "@" + t).ToList();
            if (tags.Count > 0)
            {
                _output.WriteLine("Requirement tags: " + string.Join(" ", tags));
            }
        }
    }
}
