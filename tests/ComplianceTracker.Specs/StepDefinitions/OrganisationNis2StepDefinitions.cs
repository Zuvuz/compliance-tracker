using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-142: the organisation area and NIS2, through the web app like a user.
    // UI contract: a#nav-organisation in the top bar; Views/Projects/_ProjectHeader.cshtml (#organisation-area-title,
    // #project-tabs a[data-tab]); _Nis2ScopeTab.cshtml (form#nis2-scope-check, #nis2-result[data-result], #nis2-reason);
    // _ComplianceTab.cshtml (tr.requirement-row[data-code], .requirement-title, .requirement-status, .covered-through,
    // .requirement-implements); Settings form#set-country.
    [Binding]
    public class OrganisationNis2StepDefinitions
    {
        private readonly WebScenario _web;

        public OrganisationNis2StepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [When("I open {string} from the top bar")]
        public async Task WhenIOpenFromTheTopBar(string text)
        {
            await _web.OpenAsync("/");
            var link = _web.Page!.QuerySelectorAll("#top-bar a.nav-link").FirstOrDefault(a => a.TextContent.Trim() == text)
                ?? throw new Xunit.Sdk.XunitException($"The top bar has no link '{text}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I open the NIS2 scope tab")]
        public async Task WhenIOpenTheNis2ScopeTab() => await _web.OpenAsync($"/Projects/Details/{await AreaIdAsync()}/nis2-scope");

        [When("I do the NIS2 scope check with sector {string}, {int} employees and a turnover of {int} million euro")]
        public Task WhenIDoTheScopeCheck(string sector, int employees, int turnover) => CheckAsync(sector, "None", employees, turnover);

        [When("I do the NIS2 scope check with sector {string} and service {string}, {int} employees and a turnover of {int} million euro")]
        public Task WhenIDoTheScopeCheckWithService(string sector, string service, int employees, int turnover) => CheckAsync(sector, service, employees, turnover);

        [Given("I have done the NIS2 scope check as an important entity")]
        public Task GivenIHaveDoneTheScopeCheck() => CheckAsync("Manufacturing", "None", 60, 12);

        [When("I post the NIS2 scope check anyway")]
        public async Task WhenIPostTheScopeCheckAnyway()
        {
            // The form is not shown; a form from a page that has a token is sent to the address, so the server must refuse
            var areaId = await AreaIdAsync();
            await _web.OpenAsync($"/Projects/Details/{areaId}/nis2-scope");
            var token = _web.Page!.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
            var fields = new Dictionary<string, string>
            {
                ["projectId"] = areaId.ToString(), ["sector"] = "Manufacturing", ["service"] = "None",
                ["employees"] = "60", ["turnover"] = "12", ["balanceSheet"] = "0"
            };
            if (token is not null)
            {
                fields["__RequestVerificationToken"] = token;
            }

            _web.Response = await _web.Client.PostAsync("/Projects/AssessNis2", new FormUrlEncodedContent(fields));
            await _web.OpenAsync($"/Projects/Details/{areaId}/nis2-scope");
        }

        [Given("the organisation's country is set to {string} on the Settings page")]
        public async Task GivenTheCountryIsSet(string country)
        {
            await _web.OpenAsync("/Settings");
            var form = _web.Page!.QuerySelector("form#set-country") ?? throw new Xunit.Sdk.XunitException("Settings has no country form.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["country"] = country });
        }

        [When("I try to add {string} to {string}")]
        public async Task WhenITryToAdd(string framework, string project)
        {
            // The tab does not offer it (R2), so the form is sent as a crafted request with the framework's id
            var projectId = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Projects/Details/{projectId}/frameworks");
            await using var db = _web.CreateDbContext();
            var frameworkId = await db.Frameworks.Where(f => f.Name == framework).Select(f => f.Id).SingleAsync();
            await _web.PostFormAsync("/Projects/AddFramework", new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString(),
                ["frameworkId"] = frameworkId.ToString()
            });
        }

        [When("I open the organisation's Compliance tab for {string} in {string}")]
        public async Task WhenIOpenTheOrganisationsComplianceTab(string framework, string language)
            => await OpenInAsync($"/Projects/Details/{await AreaIdAsync()}/compliance?sub={await SlugAsync(framework)}", language);

        [When("I open the Compliance tab of {string} in {string}")]
        public async Task WhenIOpenTheComplianceTabIn(string project, string language)
            => await OpenInAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance", language);

        // ---------------------------------------------------------- Then

        [Then("I see the organisation area of {string} with the tabs {string}, {string}, {string} and {string}")]
        public void ThenISeeTheOrganisationArea(string organisation, string first, string second, string third, string fourth)
        {
            Assert.Contains(organisation, _web.Page!.QuerySelector("#organisation-area-title")?.TextContent ?? "");
            var tabs = _web.Page.QuerySelectorAll("#project-tabs a").Select(a => a.TextContent.Trim()).ToList();
            Assert.All(new[] { first, second, third, fourth }, t => Assert.Contains(t, tabs));
            // Only what makes sense for the organisation (no product requirements, scans or security events yet)
            Assert.DoesNotContain("Product requirements", tabs);
            Assert.DoesNotContain("Scans", tabs);
            Assert.Equal("active fw-semibold", _web.Page.QuerySelector("#nav-organisation")?.ClassList.Where(c => c is "active" or "fw-semibold").Aggregate((a, b) => a + " " + b));
        }

        [Then("the Projects page does not list {string}")]
        public async Task ThenTheProjectsPageDoesNotList(string name)
        {
            await _web.OpenAsync("/Projects");
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("a"), a => a.TextContent.Trim() == name && (a.GetAttribute("href") ?? "").Contains("/Projects/Details/"));
        }

        [Then("the NIS2 result is {string} with the reason {string}")]
        public void ThenTheNis2ResultIs(string result, string reason)
        {
            Assert.Equal(result, _web.Page!.QuerySelector("#nis2-result")?.GetAttribute("data-result"));
            Assert.Equal(reason, _web.Page.QuerySelector("#nis2-reason")?.TextContent.Trim());
        }

        [Then("the NIS2 result is {string}")]
        public void ThenTheNis2ResultIsOnly(string result)
            => Assert.Equal(result, _web.Page!.QuerySelector("#nis2-result")?.GetAttribute("data-result"));

        [Then("there is no NIS2 scope check form")]
        public void ThenThereIsNoScopeCheckForm() => Assert.Null(_web.Page!.QuerySelector("form#nis2-scope-check"));

        [Then("the organisation follows {string} and {string}")]
        public async Task ThenTheOrganisationFollowsBoth(string first, string second)
        {
            var followed = await FollowedAsync();
            Assert.Contains(first, followed);
            Assert.Contains(second, followed);
        }

        [Then("the organisation follows {string}")]
        public async Task ThenTheOrganisationFollows(string framework) => Assert.Contains(framework, await FollowedAsync());

        [Then("the organisation does not follow {string}")]
        public async Task ThenTheOrganisationDoesNotFollow(string framework) => Assert.DoesNotContain(framework, await FollowedAsync());

        [Then("the organisation follows no framework")]
        public async Task ThenTheOrganisationFollowsNoFramework() => Assert.Empty(await FollowedAsync());

        [Then("the organisation's Compliance tab for {string} shows {string} as {string}")]
        public async Task ThenTheComplianceTabShows(string framework, string code, string status)
        {
            await WhenIOpenTheOrganisationsComplianceTab(framework, "en");
            Assert.Equal(status, Row(code).QuerySelector(".requirement-status")?.TextContent.Trim());
        }

        [Then("the organisation's Compliance tab for {string} shows {string} as {string} through {string}")]
        public async Task ThenTheComplianceTabShowsThrough(string framework, string code, string status, string through)
        {
            await ThenTheComplianceTabShows(framework, code, status);
            Assert.Equal($"through {through}", Row(code).QuerySelector(".covered-through")?.TextContent.Trim());
        }

        [Then("the organisation's Compliance tab for {string} shows that {string} implements {string}")]
        public async Task ThenTheComplianceTabShowsImplements(string framework, string code, string implements)
        {
            await WhenIOpenTheOrganisationsComplianceTab(framework, "en");
            Assert.Equal($"Implements {implements}", Row(code).QuerySelector(".requirement-implements")?.TextContent.Trim());
        }

        [Then("the requirement {string} is titled {string}")]
        public void ThenTheRequirementIsTitled(string code, string title)
        {
            var cell = Row(code).QuerySelector(".requirement-title")!;
            Assert.StartsWith(title, cell.TextContent.Trim(), StringComparison.Ordinal);
            Assert.Null(cell.QuerySelector(".not-translated"));
        }

        [Then("the Frameworks tab of {string} does not offer {string}")]
        public async Task ThenTheFrameworksTabDoesNotOffer(string project, string framework)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/frameworks");
            Assert.DoesNotContain(_web.Page!.QuerySelectorAll("#add-framework option"), o => o.TextContent.Trim() == framework);
        }

        // ---------------------------------------------------------- Helpers

        private async Task CheckAsync(string sector, string service, int employees, int turnover)
        {
            await WhenIOpenFromTheTopBar("Organisation");
            await _web.OpenAsync($"/Projects/Details/{await AreaIdAsync()}/nis2-scope");
            var form = _web.Page!.QuerySelector("form#nis2-scope-check") ?? throw new Xunit.Sdk.XunitException("The NIS2 scope tab has no check form.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["projectId"] = form.QuerySelector("input[name=projectId]")!.GetAttribute("value")!,
                ["sector"] = sector,
                ["service"] = service,
                ["employees"] = employees.ToString(),
                ["turnover"] = turnover.ToString(),
                ["balanceSheet"] = turnover.ToString()
            });
        }

        private async Task OpenInAsync(string path, string language)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.AcceptLanguage.ParseAdd(language);
            _web.Response = await _web.Client.SendAsync(request);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        private AngleSharp.Dom.IElement Row(string code)
            => _web.Page!.QuerySelectorAll("tr.requirement-row").FirstOrDefault(r => r.GetAttribute("data-code") == code)
               ?? throw new Xunit.Sdk.XunitException($"No requirement '{code}' on {_web.CurrentPath}.");

        private async Task<Guid> AreaIdAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Kind == ProjectKind.Organisation).Select(p => p.Id).SingleOrDefaultAsync() is var id && id != Guid.Empty
                ? id
                : throw new Xunit.Sdk.XunitException("The organisation area has not been opened yet.");
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project && p.Kind == ProjectKind.Product).Select(p => p.Id).SingleAsync();
        }

        private async Task<string> SlugAsync(string shortName)
        {
            await using var db = _web.CreateDbContext();
            var name = (await db.Frameworks.Select(f => f.Name).ToListAsync()).Single(n => n.Split(" – ")[0] == shortName);
            return ComplianceTracker.Application.Frameworks.FrameworkSlugs.Slug(name);
        }

        private async Task<List<string>> FollowedAsync()
        {
            await using var db = _web.CreateDbContext();
            var area = await db.Projects.SingleOrDefaultAsync(p => p.Kind == ProjectKind.Organisation);
            if (area is null)
            {
                return [];
            }

            var ids = area.Frameworks.Select(f => f.FrameworkId).ToList();
            return await db.Frameworks.Where(f => ids.Contains(f.Id)).Select(f => f.Name).ToListAsync();
        }
    }
}
