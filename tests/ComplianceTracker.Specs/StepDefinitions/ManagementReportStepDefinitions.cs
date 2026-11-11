using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-141: the management report. The PDF and Excel checks live with their downloads (ReportsAsPdfStepDefinitions,
    // TraceabilityReportStepDefinitions); this class covers requirements through the Compliance tab, like a user.
    [Binding]
    public class ManagementReportStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly HashSet<string> _stories = [];
        private readonly HashSet<string> _linked = [];

        public ManagementReportStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // Links the story to the first requirements of the CRA in the catalogue's order; already linked ones are skipped
        [Given("the first {int} CRA requirements of {string} are covered by the user story {string}")]
        public async Task GivenTheFirstCraRequirementsAreCovered(int count, string project, string story)
        {
            if (_stories.Add(story))
            {
                await _compliance.GivenTheProjectHasTheUserStory(project, story);
            }

            foreach (var code in await FirstCraCodesAsync(count))
            {
                if (_linked.Add(code))
                {
                    await _compliance.OpenSubTabWithAsync(project, code);
                    await _compliance.WhenILinkToTheRequirement(story, code);
                }
            }
        }

        private async Task<IReadOnlyList<string>> FirstCraCodesAsync(int count)
        {
            await using var db = _web.CreateDbContext();
            var frameworkIds = await db.Frameworks.Where(f => f.Name.Contains("2024/2847")).Select(f => f.Id).ToListAsync();
            var version = await db.FrameworkVersions
                .Where(v => frameworkIds.Contains(v.FrameworkId) && v.Status == FrameworkVersionStatus.Active)
                .OrderByDescending(v => v.Number)
                .Select(v => v.Id)
                .FirstAsync();
            return await db.Requirements.Where(r => r.FrameworkVersionId == version).OrderBy(r => r.Position).Take(count).Select(r => r.Code).ToListAsync();
        }
    }
}
