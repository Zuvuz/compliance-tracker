using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-069: CRA version 2 (BuiltInCraRequirementsV2) on the Compliance tab (#requirements-table tr.requirement-row)
    // and in the database
    [Binding]
    public class CraObligationsStepDefinitions
    {
        private readonly WebScenario _web;

        public CraObligationsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the requirement {string} says {string}")]
        public void ThenTheRequirementSays(string code, string title)
        {
            var row = _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
                ?? throw new Xunit.Sdk.XunitException($"The page has no requirement '{code}'.");
            Assert.Contains(title, row.TextContent);
        }

        // R6: the same codes, titles and texts, so a project can move without losing anything
        [Then("version {int} of {string} has every code of version {int} with the same text")]
        public async Task ThenVersionHasEveryCodeOf(int newer, string framework, int older)
        {
            var olderRequirements = await RequirementsAsync(framework, older);
            var newerRequirements = (await RequirementsAsync(framework, newer)).ToDictionary(r => r.Code);
            Assert.NotEmpty(olderRequirements);
            Assert.All(olderRequirements, r =>
            {
                Assert.True(newerRequirements.TryGetValue(r.Code, out var same), $"Version {newer} has no '{r.Code}'.");
                Assert.Equal((r.Title, r.Description, r.Group), (same!.Title, same.Description, same.Group));
            });
        }

        // R1: version 1 is never changed (US-035 R7)
        [Then("version {int} of {string} still has {int} requirements")]
        public async Task ThenVersionStillHas(int version, string framework, int count)
            => Assert.Equal(count, (await RequirementsAsync(framework, version)).Count);

        [Then("the activity log of {string} says {string}")]
        public async Task ThenTheActivityLogSays(string project, string text)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            var details = await db.ActivityLog
                .Where(e => e.SubjectId == projectId && e.Action == ActivityAction.ProjectFrameworkVersionChanged)
                .Select(e => e.Details)
                .ToListAsync();
            Assert.Contains(details, d => d != null && d.Contains(text));
        }

        private async Task<List<(string Code, string Title, string Description, string? Group)>> RequirementsAsync(string framework, int version)
        {
            await using var db = _web.CreateDbContext();
            var versionId = await (from f in db.Frameworks
                                   from v in f.Versions
                                   where f.Name == framework && v.Number == version
                                   select v.Id).SingleAsync();
            return (await db.Requirements.Where(r => r.FrameworkVersionId == versionId).ToListAsync())
                .Select(r => (r.Code, r.Title, r.Description, r.Group))
                .ToList();
        }
    }
}
