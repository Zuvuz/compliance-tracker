using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Shared log step for every story whose log entries point at something by name (US-006, US-008, US-011).
    // The "something" can be a project or a framework, so the step looks for both.
    [Binding]
    public class ActivityLogStepDefinitions
    {
        private readonly WebScenario _web;

        public ActivityLogStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the activity log for {string} has {string} for {string} concerning {string}")]
        public async Task ThenTheActivityLogHasConcerning(string organisationName, string action, string userName, string subjectName)
        {
            await using var db = _web.CreateDbContext();

            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var user = organisation.Users.Single(u => u.Name == userName);

            // Everything with this name the entry could be about: the organisation's project,
            // or any version of a framework (the organisation's own or a built-in one)
            var projectIds = await db.Projects
                .Where(p => p.OrganizationId == organisation.Id && p.Name == subjectName)
                .Select(p => p.Id)
                .ToListAsync();

            var frameworkVersionIds = await db.FrameworkVersions
                .Where(v => db.Frameworks.Any(f => f.Id == v.FrameworkId
                    && f.Name == subjectName
                    && (f.OwnerOrganizationId == organisation.Id || f.OwnerOrganizationId == null)))
                .Select(v => v.Id)
                .ToListAsync();

            var subjectIds = projectIds.Concat(frameworkVersionIds).ToList();
            Assert.True(subjectIds.Count > 0, $"Nothing called '{subjectName}' exists for {organisationName}.");

            var expectedAction = Enum.Parse<ActivityAction>(action);

            // Who (user), what (action), where (organisation) and about what (SubjectId)
            Assert.True(
                await db.ActivityLog.AnyAsync(e =>
                    e.OrganizationId == organisation.Id &&
                    e.UserId == user.Id &&
                    e.Action == expectedAction &&
                    e.SubjectId != null && subjectIds.Contains(e.SubjectId.Value)),
                $"No '{action}' log entry by {userName} concerning '{subjectName}'.");
        }
    }
}
