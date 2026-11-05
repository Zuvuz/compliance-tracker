using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-009, US-010: invitations, roles and the system admin, through the web app with test sign-in.
    // UI contract: the Users page /Users (#organisation-users rows data-member, forms change-role, #create-invitation,
    // #invitation-link, revoke-invitation), /Invitations/Accept/{token} (#invitation-problem, #accept-invitation),
    // and /System (#system-organisations, #system-members rows data-member with system-change-role forms).
    [Binding]
    public class RolesAndInvitationsStepDefinitions
    {
        private const string Organisation = "Acme ApS";

        private readonly WebScenario _web;
        private string? _invitationPath;
        private string? _systemAdmin;

        public RolesAndInvitationsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I have created an invitation link with the role {string}")]
        [When("I create an invitation link with the role {string}")]
        public async Task WhenICreateAnInvitationLink(string role)
        {
            await OpenUsersAsync();
            var form = _web.Page!.QuerySelector("#create-invitation")
                ?? throw new Xunit.Sdk.XunitException("The Users tab has no invitation form.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["role"] = role });

            var link = _web.Page!.QuerySelector<IHtmlInputElement>("#invitation-link")?.Value
                ?? throw new Xunit.Sdk.XunitException("No invitation link was shown.");
            _invitationPath = new Uri(link).PathAndQuery;
        }

        [Given("{string} has opened the invitation link and joined")]
        [When("{string} opens the invitation link and joins")]
        public async Task WhenOpensTheInvitationLinkAndJoins(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync(_invitationPath));
            var token = page.QuerySelector<IHtmlInputElement>("#accept-invitation input[name='__RequestVerificationToken']")?.Value
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' cannot accept: {page.QuerySelector("#invitation-problem")?.TextContent.Trim()}");

            var response = await client.PostAsync(_invitationPath,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            // Lands on the organisation's projects, not on "create your organisation" (US-045)
            Assert.Equal("/Projects", response.RequestMessage!.RequestUri!.AbsolutePath);
        }

        [When("{string} opens the invitation link")]
        public async Task WhenOpensTheInvitationLink(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync(_invitationPath);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        // The organisation admin (signed in on the main client) invites, the person joins in their own browser
        [Given("{string} has joined {string} as {string}")]
        public async Task GivenHasJoinedAs(string userName, string organisation, string role)
        {
            Assert.Equal(Organisation, organisation);
            await WhenICreateAnInvitationLink(role);
            await WhenOpensTheInvitationLinkAndJoins(userName);
        }

        [When("I revoke the invitation")]
        public async Task WhenIRevokeTheInvitation()
        {
            await OpenUsersAsync();
            var form = _web.Page!.QuerySelector("form.revoke-invitation")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I change the role of {string} to {string}")]
        public async Task WhenIChangeTheRoleOf(string userName, string role)
        {
            await OpenUsersAsync();
            var row = _web.Page!.QuerySelector($"#organisation-users tr[data-member='{userName}']");
            var form = row?.QuerySelector("form.change-role");
            if (form is null)
            {
                // Roles that may not change roles cannot open the page (US-045 R2); send the request directly (US-010 R5).
                // The antiforgery token comes from a page the role may open.
                var memberId = await MemberIdAsync(userName);
                await _web.OpenAsync("/Projects");
                await _web.PostFormAsync("/Users/ChangeRole",
                    new Dictionary<string, string> { ["memberId"] = memberId.ToString(), ["role"] = role });
                return;
            }

            var fields = HiddenFields(form);
            fields["role"] = role;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Given("{string} is the system admin")]
        public void GivenIsTheSystemAdmin(string userName)
            => _systemAdmin = userName;

        [When("the system admin changes the role of {string} in {string} to {string}")]
        public async Task WhenTheSystemAdminChangesTheRole(string userName, string organisation, string role)
        {
            using var client = SystemAdminClient();
            var list = await WebScenario.ParseAsync(await client.GetAsync("/System"));
            var link = list.QuerySelector($"#system-organisations tr[data-organisation='{organisation}'] a")!.GetAttribute("href")!;
            var page = await WebScenario.ParseAsync(await client.GetAsync(link));
            var form = page.QuerySelector($"#system-members tr[data-member='{userName}'] form.system-change-role")!;

            var fields = HiddenFields(form);
            fields["role"] = role;
            fields["__RequestVerificationToken"] = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            var response = await client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [When("the system admin opens the page of {string}")]
        public async Task WhenTheSystemAdminOpensThePageOf(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            using var client = SystemAdminClient();
            _web.Response = await client.GetAsync($"/Projects/Details/{id}");
        }

        [When("I open the system pages")]
        public async Task WhenIOpenTheSystemPages()
        {
            using var client = _web.CreateClient(signedInAs: "Anna", followRedirects: false);
            _web.Response = await client.GetAsync("/System");
        }

        // ---------------------------------------------------------- Then

        [Then("{string} is a member of {string} with the role {string}")]
        public async Task ThenIsAMemberWithTheRole(string userName, string organisation, string role)
        {
            await using var db = _web.CreateDbContext();
            var org = await db.Organizations.SingleAsync(o => o.Name == organisation);
            Assert.Equal(Enum.Parse<UserRole>(role), org.Users.Single(u => u.Name == userName).Role);
        }

        [Then("the invitation says {string}")]
        public void ThenTheInvitationSays(string text)
            => Assert.Equal(text, _web.Page!.QuerySelector("#invitation-problem")?.TextContent.Trim());

        [Then("the database does not contain the invitation's token")]
        public async Task ThenTheDatabaseDoesNotContainTheToken()
        {
            var token = _invitationPath!.Split('/').Last();
            await using var db = _web.CreateDbContext();
            var stored = await db.Invitations.Select(i => i.TokenHash).SingleAsync();
            Assert.NotEqual(token, stored);
            Assert.DoesNotContain(token, stored);
        }

        [Then("there is no {string} button")]
        public void ThenThereIsNoButton(string text)
            => Assert.DoesNotContain(text, _web.Page!.QuerySelectorAll("a, button").Select(e => e.TextContent.Trim()));

        [Then("the {string} log entry by {string} has the role {string}")]
        public async Task ThenTheLogEntryHasTheRole(string action, string userName, string role)
        {
            var memberId = await MemberIdAsync(userName);
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<ActivityAction>(action);
            var roles = await db.ActivityLog.Where(e => e.Action == expected && e.UserId == memberId).Select(e => e.Role).ToListAsync();
            Assert.Contains(Enum.Parse<UserRole>(role), roles.Select(r => r!.Value));
        }

        [Then("access is denied")]
        public void ThenAccessIsDenied()
            => Assert.True(_web.Response!.StatusCode is HttpStatusCode.Forbidden
                || _web.Response.Headers.Location?.ToString().Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected access denied, got {(int)_web.Response.StatusCode}.");

        // ---------------------------------------------------------- Helpers

        private HttpClient SystemAdminClient()
            => _web.CreateClient(signedInAs: _systemAdmin ?? throw new Xunit.Sdk.XunitException("No system admin in the scenario."),
                realmRoles: "system-admin");

        private async Task OpenUsersAsync()
            => await _web.OpenAsync("/Users");

        private async Task<Guid> OrganisationIdAsync()
        {
            await using var db = _web.CreateDbContext();
            return await db.Organizations.Where(o => o.Name == Organisation).Select(o => o.Id).SingleAsync();
        }

        private async Task<Guid> MemberIdAsync(string userName)
        {
            await using var db = _web.CreateDbContext();
            return (await db.Organizations.SingleAsync(o => o.Name == Organisation)).Users.Single(u => u.Name == userName).Id;
        }

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
