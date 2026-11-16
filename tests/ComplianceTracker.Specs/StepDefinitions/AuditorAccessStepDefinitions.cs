using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-182: each person has their own "browser" (cookies kept). The Reports tab's #auditor-access with
    // form#grant-auditor-access, #auditor-link and #auditor-grants rows (data-email) with form.revoke-auditor-access.
    [Binding]
    public sealed class AuditorAccessStepDefinitions : IDisposable
    {
        private readonly WebScenario _web;
        private readonly Dictionary<string, HttpClient> _browsers = new();
        private IDocument? _page;
        private string? _link;

        public AuditorAccessStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        public void Dispose()
        {
            foreach (var browser in _browsers.Values)
            {
                browser.Dispose();
            }
        }

        [Given("{string} has an organisation {string} with the projects {string} and {string}")]
        public async Task GivenAnOrganisationWithProjects(string userName, string organisation, string first, string second)
        {
            await _web.CreateOrganisationAsync(userName, organisation);
            foreach (var project in new[] { first, second })
            {
                var page = await OpenAsync(userName, "/Projects/Create");
                (await PostAsync(userName, "/Projects/Create", page, new()
                {
                    ["Name"] = project, ["Description"] = "", ["IntendedPurpose"] = "Test purpose", ["Scope"] = "", ["LifecyclePhase"] = "Planning"
                })).EnsureSuccessStatusCode();
            }
        }

        [Given("{string} has given {string} access to {string} from {string} to {string}")]
        public async Task GivenHasGivenAccess(string userName, string email, string project, string from, string until)
        {
            await WhenGivesAccess(userName, email, project, from, until);
            _link = _page!.QuerySelector<IHtmlInputElement>("#auditor-link")?.Value
                ?? throw new Xunit.Sdk.XunitException($"No link was shown: {_page.QuerySelector(".validation-summary-errors")?.TextContent}");
        }

        [When("{string} gives {string} access to {string} from {string} to {string}")]
        public async Task WhenGivesAccess(string userName, string email, string project, string from, string until)
        {
            var page = await OpenReportsAsync(userName, project);
            var form = page.QuerySelector("form#grant-auditor-access") ?? throw new Xunit.Sdk.XunitException("No form to give an auditor access.");
            var response = await PostAsync(userName, form.GetAttribute("action")!, page, new()
            {
                ["projectId"] = (await ProjectIdAsync(project)).ToString(), ["email"] = email, ["from"] = from, ["until"] = until
            });
            _page = await WebScenario.ParseAsync(response);
        }

        [Given("{string} has accepted the auditor link")]
        public async Task GivenHasAccepted(string userName)
        {
            var path = new Uri(_link!).PathAndQuery;
            var page = await OpenAsync(userName, path);
            var token = page.QuerySelector<IHtmlInputElement>("#accept-invitation input[name='__RequestVerificationToken']")?.Value
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' cannot accept: {page.QuerySelector("#invitation-problem")?.TextContent.Trim()}");
            (await Browser(userName).PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })))
                .EnsureSuccessStatusCode();
        }

        [When("{string} revokes the access of {string} to {string}")]
        public async Task WhenRevokes(string userName, string email, string project)
        {
            var page = await OpenReportsAsync(userName, project);
            var form = page.QuerySelector($"#auditor-grants tr[data-email='{email}'] form.revoke-auditor-access")
                ?? throw new Xunit.Sdk.XunitException($"'{email}' cannot be revoked.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            (await PostAsync(userName, form.GetAttribute("action")!, page, fields)).EnsureSuccessStatusCode();
        }

        [When("{string} looks at the auditor access of {string}")]
        public async Task WhenOpensReports(string userName, string project) => _page = await OpenReportsAsync(userName, project);

        [Then("{string} can open the project {string}")]
        [When("{string} can open the project {string}")]
        public async Task ThenCanOpen(string userName, string project)
        {
            var response = await Browser(userName).GetAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(project, (await WebScenario.ParseAsync(response)).QuerySelector("#project-name")?.TextContent ?? "");
        }

        [Then("{string} cannot open the project {string}")]
        public async Task ThenCannotOpenTheProject(string userName, string project)
        {
            var response = await Browser(userName).GetAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            var shown = (await WebScenario.ParseAsync(response)).QuerySelector("#project-name")?.TextContent ?? "";
            Assert.False(response.StatusCode == System.Net.HttpStatusCode.OK && shown.Contains(project), $"'{userName}' could open {project}.");
        }

        [Then("{string} cannot open {string}")]
        public async Task ThenCannotOpen(string userName, string path)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            var response = await client.GetAsync(path);
            Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [Then("{string} is told that the access to {string} has ended")]
        public async Task ThenIsTold(string userName, string organisation)
        {
            var page = await OpenAsync(userName, "/Projects");
            Assert.Contains($"Your access to {organisation} has ended", page.QuerySelector("#access-ended")?.TextContent ?? "(no message)");
        }

        [Then("{string} sees the error {string}")]
        public void ThenSeesTheError(string userName, string message)
            => Assert.Contains(message, _page!.QuerySelector(".validation-summary-errors")?.TextContent ?? "(no error)");

        [Then("there is no form to give an auditor access")]
        public void ThenNoForm() => Assert.Null(_page!.QuerySelector("form#grant-auditor-access"));

        // ---------------------------------------------------------- Helpers

        private HttpClient Browser(string userName)
            => _browsers.TryGetValue(userName, out var browser) ? browser : _browsers[userName] = _web.CreateClient(signedInAs: userName);

        private async Task<IDocument> OpenAsync(string userName, string path)
            => await WebScenario.ParseAsync(await Browser(userName).GetAsync(path));

        private async Task<IDocument> OpenReportsAsync(string userName, string project)
            => await OpenAsync(userName, $"/Projects/Details/{await ProjectIdAsync(project)}/reports");

        private async Task<HttpResponseMessage> PostAsync(string userName, string path, IDocument page, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            return await Browser(userName).PostAsync(path, new FormUrlEncodedContent(fields));
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
