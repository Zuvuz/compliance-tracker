using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-102: the organisation's Activity page (/Activity, Views/Shared/_ActivityLog.cshtml with #organisation-activity,
    // rows .activity-row with .activity-action and .activity-about, and the filter select[name=project])
    [Binding]
    public class OrganisationActivityStepDefinitions
    {
        private readonly WebScenario _web;
        private IDocument? _otherPage;

        public OrganisationActivityStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the organisation's activity")]
        public Task WhenIOpenTheOrganisationsActivity()
            => _web.OpenAsync("/Activity");

        // "Not about a project", or a project's name
        [When("I filter the organisation's activity on {string}")]
        public async Task WhenIFilterOn(string project)
        {
            await _web.OpenAsync("/Activity");
            var option = _web.Page!.QuerySelectorAll("select[name=project] option").FirstOrDefault(o => o.TextContent.Trim() == project)
                ?? throw new Xunit.Sdk.XunitException($"The project filter has no \"{project}\".");
            await _web.OpenAsync("/Activity?project=" + Uri.EscapeDataString(option.GetAttribute("value")!));
        }

        [When("I search the organisation's activity for {string}")]
        public Task WhenISearch(string text)
            => _web.OpenAsync("/Activity?q=" + Uri.EscapeDataString(text));

        [When("{string} opens the organisation's activity")]
        public async Task WhenOpensTheOrganisationsActivity(string user)
        {
            var client = _web.CreateClient(signedInAs: user, followRedirects: false);
            _web.Response = await client.GetAsync("/Activity");
            _otherPage = await WebScenario.ParseAsync(_web.Response);
        }

        [When("{string} searches the organisation's activity for {string}")]
        public async Task WhenSearches(string user, string text)
        {
            using var client = _web.CreateClient(signedInAs: user);
            _otherPage = await WebScenario.ParseAsync(await client.GetAsync("/Activity?q=" + Uri.EscapeDataString(text)));
        }

        [Then("I see an {string} entry")]
        [Then("I see a {string} entry")]
        public void ThenISeeAnEntry(string action)
            => Assert.Contains(Rows(_web.Page!), r => Action(r) == action);

        [Then("I see a {string} entry about {string}")]
        public void ThenISeeAnEntryAbout(string action, string about)
            => Assert.Contains(Rows(_web.Page!), r => Action(r) == action && r.QuerySelector(".activity-about")?.TextContent.Trim() == about);

        [Then("I see no {string} entry")]
        public void ThenISeeNoEntry(string action)
            => Assert.DoesNotContain(Rows(_web.Page!), r => Action(r) == action);

        [Then("{string} sees the organisation's activity")]
        public void ThenSeesTheOrganisationsActivity(string _)
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, _web.Response!.StatusCode);
            Assert.NotNull(_otherPage!.QuerySelector("#organisation-activity"));
        }

        // R7: Bo's page only has Beta ApS's entries, so nothing about Acme's project or its members
        [Then("{string} sees no entries from {string}")]
        public async Task ThenSeesNoEntriesFrom(string user, string organisation)
        {
            await using var db = _web.CreateDbContext();
            var acme = await db.Organizations.SingleAsync(o => o.Name == organisation);
            var acmeNames = acme.Users.Select(u => u.Name)
                .Concat(await db.Projects.Where(p => p.OrganizationId == acme.Id).Select(p => p.Name).ToListAsync())
                .ToHashSet();

            Assert.NotNull(_otherPage!.QuerySelector("#organisation-activity"));
            Assert.DoesNotContain(Rows(_otherPage!), r =>
                acmeNames.Contains(r.QuerySelector(".activity-user")?.TextContent.Trim() ?? "")
                || acmeNames.Contains(r.QuerySelector(".activity-about")?.TextContent.Trim() ?? ""));
        }

        private static IEnumerable<IElement> Rows(IDocument page)
            => page.QuerySelectorAll("#organisation-activity .activity-row");

        private static string? Action(IElement row) => row.QuerySelector(".activity-action")?.TextContent.Trim();
    }
}
