using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-187: each person has their own "browser" (an HttpClient that keeps cookies), so the chosen organisation is kept
    // per person like in real life. Pages: the switcher #organisation-switcher with form.switch-organisation[data-organisation],
    // and "My customers" (#my-customers rows with data-organisation).
    [Binding]
    public sealed class ConsultantStepDefinitions : IDisposable
    {
        private readonly WebScenario _web;
        private readonly Dictionary<string, HttpClient> _browsers = new();
        private HttpResponseMessage? _response;
        private IDocument? _page;

        public ConsultantStepDefinitions(WebScenario web)
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

        // ---------------------------------------------------------- Given / When

        [Given("{string} has created the organisation {string} with the project {string}")]
        public async Task GivenHasCreatedWithProject(string userName, string organisation, string project)
        {
            await _web.CreateOrganisationAsync(userName, organisation);
            await CreateProjectAsync(userName, project);
        }

        [Given("{string} has invited {string} to {string} as {string}")]
        public async Task GivenHasInvited(string admin, string userName, string organisation, string role)
        {
            var path = await InvitationPathAsync(admin, role);
            var page = await OpenAsync(userName, path);
            var token = page.QuerySelector<IHtmlInputElement>("#accept-invitation input[name='__RequestVerificationToken']")?.Value
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' cannot join {organisation}: {page.QuerySelector("#invitation-problem")?.TextContent.Trim()}");
            var response = await Browser(userName).PostAsync(path,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            response.EnsureSuccessStatusCode();
        }

        [When("{string} invites {string} to {string} as {string}")]
        public async Task WhenInvites(string admin, string userName, string organisation, string role)
            => _page = await OpenAsync(userName, await InvitationPathAsync(admin, role));

        [Given("{string} has switched to {string}")]
        [When("{string} switches to {string}")]
        public async Task WhenSwitches(string userName, string organisation)
        {
            var page = await OpenAsync(userName, "/Projects");
            var form = page.QuerySelector($"#organisation-switcher form.switch-organisation[data-organisation='{organisation}']")
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' has no switch to {organisation}.");
            _response = await PostAsync(userName, form, page);
            _response.EnsureSuccessStatusCode();
        }

        [When("{string} sets the chosen organisation to {string} by hand")]
        public async Task WhenSetsTheCookie(string userName, string organisation)
        {
            var id = await OrganisationIdAsync(organisation);
            Browser(userName).DefaultRequestHeaders.Add("Cookie", $"ct_org={id}");
        }

        [When("{string} removes {string} from {string}")]
        public async Task WhenRemoves(string admin, string userName, string organisation)
        {
            var page = await OpenAsync(admin, "/Users");
            var form = page.QuerySelector($"#organisation-users tr[data-member='{userName}'] form.remove-member")
                ?? throw new Xunit.Sdk.XunitException($"The Users page of {organisation} has no way to remove {userName}.");
            (await PostAsync(admin, form, page)).EnsureSuccessStatusCode();
        }

        [When("{string} creates the project {string} in {string}")]
        public async Task WhenCreatesTheProject(string userName, string project, string organisation)
        {
            // With one organisation there is nothing to switch to
            if ((await OpenAsync(userName, "/Projects")).QuerySelector("#organisation-switcher") is not null)
            {
                await WhenSwitches(userName, organisation);
            }

            await CreateProjectAsync(userName, project);
        }

        [When("{string} opens the customer overview")]
        public async Task WhenOpensMyCustomers(string userName)
            => _page = await OpenAsync(userName, "/Organizations/Customers");

        [When("{string} opens the projects")]
        public async Task WhenOpensTheProjects(string userName)
            => _page = await OpenAsync(userName, "/Projects");

        // ---------------------------------------------------------- Then

        [Then("{string} sees the project {string} and not {string}")]
        public async Task ThenSeesTheProject(string userName, string visible, string hidden)
        {
            var text = (await OpenAsync(userName, "/Projects")).Body!.TextContent;
            Assert.Contains(visible, text);
            Assert.DoesNotContain(hidden, text);
        }

        [Then("{string} cannot switch to {string}")]
        public async Task ThenCannotSwitch(string userName, string organisation)
        {
            var page = await OpenAsync(userName, "/Projects");
            Assert.Null(page.QuerySelector($"form.switch-organisation[data-organisation='{organisation}']"));
            var token = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            var response = await Browser(userName).PostAsync("/Organizations/Switch", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["organizationId"] = (await OrganisationIdAsync(organisation)).ToString(),
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Then("the invitation to {string} says {string}")]
        public void ThenTheInvitationSays(string userName, string text)
            => Assert.Contains(text, _page!.QuerySelector("#invitation-problem")?.TextContent ?? "");

        [Then("the activity log of {string} has an entry by {string} as {string}")]
        public async Task ThenTheLogHasAnEntry(string organisation, string userName, string role)
        {
            await using var db = _web.CreateDbContext();
            var id = await OrganisationIdAsync(organisation);
            var userId = await UserIdAsync(id, userName);
            var expected = Enum.Parse<Domain.Users.UserRole>(role);
            Assert.True(await db.ActivityLog.AnyAsync(e => e.OrganizationId == id && e.UserId == userId && e.Role == expected),
                $"{organisation}'s log has nothing by {userName} as {role}.");
        }

        [Then("the activity log of {string} has no entry by {string}")]
        public async Task ThenTheLogHasNoEntry(string organisation, string userName)
        {
            await using var db = _web.CreateDbContext();
            var id = await OrganisationIdAsync(organisation);
            var externalId = TestUsers.Identity(userName).ExternalId;
            var ids = await db.Organizations.SelectMany(o => o.Users).Where(u => u.ExternalId == externalId).Select(u => u.Id).ToListAsync();
            Assert.False(await db.ActivityLog.AnyAsync(e => e.OrganizationId == id && e.UserId != null && ids.Contains(e.UserId.Value)));
        }

        [Then("the customer overview lists {string} and {string}")]
        public void ThenMyCustomersLists(string first, string second)
        {
            var rows = _page!.QuerySelectorAll("#my-customers tbody tr").Select(r => r.GetAttribute("data-organisation")).ToList();
            Assert.Equal([first, second], rows);
            Assert.All(_page.QuerySelectorAll("#my-customers .customer-percent"), c => Assert.EndsWith("%", c.TextContent.Trim()));
        }

        [Then("there is no organisation switcher")]
        public void ThenThereIsNoSwitcher()
            => Assert.Null(_page!.QuerySelector("#organisation-switcher"));

        // ---------------------------------------------------------- Helpers

        private HttpClient Browser(string userName)
            => _browsers.TryGetValue(userName, out var browser) ? browser : _browsers[userName] = _web.CreateClient(signedInAs: userName);

        private async Task<IDocument> OpenAsync(string userName, string path)
        {
            _response = await Browser(userName).GetAsync(path);
            return await WebScenario.ParseAsync(_response);
        }

        private async Task<HttpResponseMessage> PostAsync(string userName, IElement form, IDocument page)
        {
            var fields = form.QuerySelectorAll("input[type=hidden]")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["__RequestVerificationToken"] = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            return await Browser(userName).PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
        }

        private async Task CreateProjectAsync(string userName, string project)
        {
            var page = await OpenAsync(userName, "/Projects/Create");
            var token = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            (await Browser(userName).PostAsync("/Projects/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = project,
                ["Description"] = "",
                ["IntendedPurpose"] = "Test purpose",
                ["Scope"] = "",
                ["LifecyclePhase"] = "Planning",
                ["__RequestVerificationToken"] = token
            }))).EnsureSuccessStatusCode();
        }

        private async Task<string> InvitationPathAsync(string admin, string role)
        {
            var page = await OpenAsync(admin, "/Users");
            var form = page.QuerySelector("#create-invitation") ?? throw new Xunit.Sdk.XunitException($"{admin} cannot invite.");
            var token = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            var response = await Browser(admin).PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = role,
                ["__RequestVerificationToken"] = token
            }));
            var link = (await WebScenario.ParseAsync(response)).QuerySelector<IHtmlInputElement>("#invitation-link")?.Value
                ?? throw new Xunit.Sdk.XunitException("No invitation link was shown.");
            return new Uri(link).PathAndQuery;
        }

        private async Task<Guid> OrganisationIdAsync(string organisation)
        {
            await using var db = _web.CreateDbContext();
            return await db.Organizations.Where(o => o.Name == organisation).Select(o => o.Id).SingleAsync();
        }

        private async Task<Guid> UserIdAsync(Guid organisationId, string userName)
        {
            await using var db = _web.CreateDbContext();
            var externalId = TestUsers.Identity(userName).ExternalId;
            return await db.Organizations.Where(o => o.Id == organisationId)
                .SelectMany(o => o.Users.Where(u => u.ExternalId == externalId).Select(u => u.Id)).SingleAsync();
        }
    }
}
