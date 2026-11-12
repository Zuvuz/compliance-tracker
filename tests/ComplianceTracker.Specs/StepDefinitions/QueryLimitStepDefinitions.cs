using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-139: each important page has a limit on its database queries, and the number does not grow with the data.
    // The data is added straight to the database, so a scenario can have hundreds of rows in a moment.
    [Binding]
    public class QueryLimitStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private Dictionary<string, int> _small = [];

        public QueryLimitStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        [Given("{string} has {int} user stories, {int} risks and {int} pieces of evidence on its requirements")]
        [When("{string} gets {int} user stories, {int} risks and {int} pieces of evidence on its requirements")]
        public async Task GivenTheProjectHasData(string project, int stories, int risks, int evidence)
        {
            await using var db = _web.CreateDbContext();
            var p = await db.Projects.SingleAsync(x => x.Name == project);
            var anna = (await db.Organizations.SingleAsync()).Users.First().Id;
            var versions = p.Frameworks.Select(f => f.FrameworkVersionId).ToList();
            var requirements = await db.Requirements.Where(r => versions.Contains(r.FrameworkVersionId)).Select(r => r.Id).ToListAsync();
            var now = _clock.GetUtcNow().UtcDateTime;
            var start = await db.WorkItems.CountAsync(w => w.ProjectId == p.Id);

            for (var i = 0; i < stories; i++)
            {
                var n = start + i + 1;
                var item = WorkItem.Create(p.Id, WorkItemSource.GitHub,
                    new ExternalWorkItem($"#{n}", $"https://github.com/acme/portal/issues/{n}", $"[US-{n:000}] Story {n}", "As a user …", "", "", "user-story", n % 3 == 0), now);
                db.WorkItems.Add(item);
                db.RequirementLinks.Add(RequirementLink.Create(p.Id, requirements[i % requirements.Count], item.Id, anna, now));
            }

            for (var i = 0; i < risks; i++)
            {
                db.Risks.Add(Risk.Create(p.Id, $"Risk {start + i + 1}", "A risk", i % 5 + 1, (i + 2) % 5 + 1, anna, RiskTreatment.Reduce, anna, now));
            }

            for (var i = 0; i < evidence; i++)
            {
                var item = Evidence.ForLink(p.Id, $"Evidence {start + i + 1}", $"https://example.com/evidence/{start + i + 1}", null, anna, null, now);
                db.Evidence.Add(item);
                db.EvidenceLinks.Add(EvidenceLink.Create(p.Id, item.Id, requirements[i % requirements.Count], anna, null, now));
            }

            await db.SaveChangesAsync();
        }

        [When("the organisation gets {int} more projects that use the same framework")]
        public async Task WhenTheOrganisationGetsMoreProjects(int count)
        {
            await using var db = _web.CreateDbContext();
            var portal = await db.Projects.SingleAsync(x => x.Name == Project);
            var framework = portal.Frameworks.First();
            var anna = (await db.Organizations.SingleAsync()).Users.First().Id;
            var version = await db.FrameworkVersions.SingleAsync(v => v.Id == framework.FrameworkVersionId);
            var catalogue = await db.Frameworks.SingleAsync(f => f.Id == framework.FrameworkId);
            for (var i = 1; i <= count; i++)
            {
                var project = Domain.Projects.Project.Create(portal.OrganizationId, $"Extra project {i}", null, "A product", null, Domain.Projects.LifecyclePhase.Development);
                project.UseFramework(catalogue, version, anna, _clock.GetUtcNow().UtcDateTime);
                db.Projects.Add(project);
            }

            await db.SaveChangesAsync();
        }

        [Given("I count the queries of the important pages")]
        public async Task WhenICount() => _small = await CountAsync();

        // R1–R3: under the limit with much data, and not more than with little data (±2)
        [Then("each page stays within its limit and does not grow with the data:")]
        public async Task ThenEachPageStaysWithin(DataTable limits)
        {
            var large = await CountAsync();
            var problems = new List<string>();
            foreach (var row in limits.Rows)
            {
                var page = row["Page"];
                var limit = int.Parse(row["Limit"]);
                if (large[page] > limit)
                {
                    problems.Add($"{page}: {large[page]} queries, the limit is {limit}");
                }

                if (Math.Abs(large[page] - _small[page]) > 2)
                {
                    problems.Add($"{page}: {_small[page]} queries with little data, {large[page]} with much (it grows with the data)");
                }
            }

            Assert.True(problems.Count == 0,
                string.Join("\n", problems) + "\nAll: " + string.Join(", ", large.Select(p => $"{p.Key} {_small.GetValueOrDefault(p.Key)}→{p.Value}")));
        }

        // The pages of R1, each opened like a user does
        private async Task<Dictionary<string, int>> CountAsync()
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
            var pages = new Dictionary<string, string>
            {
                ["Dashboard"] = "/",
                ["Projects"] = "/Projects",
                ["Overview"] = $"/Projects/Details/{id}",
                ["Compliance"] = $"/Projects/Details/{id}/compliance",
                ["Evidence"] = $"/Projects/Details/{id}/evidence",
                ["Risks"] = $"/Projects/Details/{id}/risks",
                ["Work items"] = $"/Projects/Details/{id}/product-requirements",
                ["Reports"] = $"/Projects/Details/{id}/reports",
                ["Project activity"] = $"/Projects/Details/{id}/activity",
                ["Frameworks"] = "/Frameworks",
                ["Organisation activity"] = "/Activity",
                ["Traceability download"] = $"/Projects/Traceability/{id}?format=xlsx"
            };

            var counts = new Dictionary<string, int>();
            foreach (var (page, address) in pages)
            {
                var response = await _web.Client.GetAsync(address);
                response.EnsureSuccessStatusCode();
                counts[page] = _web.QueryCounts.For(response.RequestMessage!.RequestUri!.AbsolutePath);
            }

            return counts;
        }
    }
}
