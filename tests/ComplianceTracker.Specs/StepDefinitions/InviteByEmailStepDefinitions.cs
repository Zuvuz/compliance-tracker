using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-196: invitations by e-mail from the Users page. Each person has their own "browser" (cookies kept).
    // UI contract: Views/Users/Index.cshtml (#create-invitation with email, role, accessUntil, onlyProjectId; #invitation-link;
    // #open-invitations .invitation-email; #organisation-users rows data-member with .access-until and form.set-access-until),
    // Views/Invitations (#accept-invitation, #invitation-problem). The mails go to the pretend mail server (FakeMail).
    [Binding]
    public sealed partial class InviteByEmailStepDefinitions : IDisposable
    {
        private readonly WebScenario _web;
        private readonly Dictionary<string, HttpClient> _browsers = new();
        private IDocument? _page;
        private string? _mailedPath;

        public InviteByEmailStepDefinitions(WebScenario web)
        {
            _web = web;
            Mail = _web.App.Factory.Services.GetRequiredService<FakeMail>();
            Mail.Clear(); // the app is shared between scenarios, the mails are not
        }

        // Kept, so it can be reset when the scenario ends (the services may be gone by then)
        private FakeMail Mail { get; }

        public void Dispose()
        {
            Mail.IsConfigured = true;
            foreach (var browser in _browsers.Values)
            {
                browser.Dispose();
            }
        }

        // ---------------------------------------------------------- Given / When

        [Given("the mail server is not set up")]
        public void GivenTheMailServerIsNotSetUp() => Mail.IsConfigured = false;

        [When("{string} invites {string} as {string} until {string}")]
        public Task WhenInvites(string admin, string email, string role, string until)
            => InviteAsync(admin, email, role, until, project: null);

        [When("{string} invites {string} as {string} until {string} for the project {string}")]
        public Task WhenInvitesForTheProject(string admin, string email, string role, string until, string project)
            => InviteAsync(admin, email, role, until, project);

        [Given("{string} has invited {string} as {string} until {string} and {string} has accepted")]
        public async Task GivenHasInvitedAndAccepted(string admin, string email, string role, string until, string invitee)
        {
            await InviteAsync(admin, email, role, until, project: null);
            await WhenAcceptsTheMailedInvitation(invitee);
        }

        [When("{string} accepts the mailed invitation")]
        public async Task WhenAcceptsTheMailedInvitation(string userName)
        {
            await WhenOpensTheMailedInvitation(userName);
            var token = _page!.QuerySelector<IHtmlInputElement>("#accept-invitation input[name='__RequestVerificationToken']")?.Value
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' cannot accept: {_page.QuerySelector("#invitation-problem")?.TextContent.Trim()}");
            (await Browser(userName).PostAsync(_mailedPath!, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })))
                .EnsureSuccessStatusCode();
        }

        [When("{string} opens the mailed invitation")]
        public async Task WhenOpensTheMailedInvitation(string userName)
        {
            var body = Mail.Sent.LastOrDefault().Body ?? throw new Xunit.Sdk.XunitException("No mail was sent.");
            _mailedPath = LinkInMail().Match(body) is { Success: true } match ? match.Value : throw new Xunit.Sdk.XunitException($"No link in the mail: {body}");
            _page = await OpenAsync(userName, _mailedPath);
        }

        [When("{string} sets the end date of {string} to {string}")]
        public async Task WhenSetsTheEndDate(string admin, string member, string until)
        {
            var page = await OpenAsync(admin, "/Users");
            var form = page.QuerySelector($"#organisation-users tr[data-member='{member}'] form.set-access-until")
                ?? throw new Xunit.Sdk.XunitException($"'{member}' has no end date to set.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["accessUntil"] = until;
            _page = await WebScenario.ParseAsync(await PostAsync(admin, form.GetAttribute("action")!, page, fields));
        }

        // ---------------------------------------------------------- Then

        [Then("a mail with the invitation link is sent to {string}")]
        public void ThenAMailIsSent(string email)
        {
            var mail = Assert.Single(Mail.Sent);
            Assert.Equal(email, mail.To);
            Assert.Matches(LinkInMail(), mail.Body);
            Assert.Contains($"Sign in with {email}", mail.Body);
        }

        [Then("no mail is sent")]
        public void ThenNoMailIsSent() => Assert.Empty(Mail.Sent);

        [Then("{string} is shown the invitation link")]
        public void ThenIsShownTheLink(string admin)
        {
            Assert.Contains("/Invitations/Accept/", _page!.QuerySelector<IHtmlInputElement>("#invitation-link")?.Value ?? "(no link)");
            Assert.Null(_page.QuerySelector("#invitation-mailed"));
        }

        [Then("{string} sees the open invitation to {string} until {string}")]
        public void ThenSeesTheOpenInvitation(string admin, string email, string until)
        {
            Assert.Contains("mailed to " + email, _page!.QuerySelector("#invitation-mailed")?.TextContent ?? "(not mailed)");
            var row = _page.QuerySelectorAll("#open-invitations .invitation-email").Select(c => c.TextContent.Trim()).ToList();
            Assert.Contains($"{email} · until {until}", row);
        }

        [Then("the member {string} has access until {string}")]
        public async Task ThenTheMemberHasAccessUntil(string member, string until)
        {
            var page = await OpenAsync("Anna", "/Users");
            Assert.Equal($"until {until}", page.QuerySelector($"#organisation-users tr[data-member='{member}'] .access-until")?.TextContent.Trim());
        }

        [Then("{string} is told {string}")]
        public void ThenIsTold(string userName, string message)
        {
            var shown = _page!.QuerySelector("#invitation-problem")?.TextContent ?? _page.QuerySelector(".validation-summary-errors")?.TextContent ?? "(nothing)";
            Assert.Contains(message, shown);
        }

        // ---------------------------------------------------------- Helpers

        private async Task InviteAsync(string admin, string email, string role, string until, string? project)
        {
            var page = await OpenAsync(admin, "/Users");
            var form = page.QuerySelector("#create-invitation") ?? throw new Xunit.Sdk.XunitException($"{admin} cannot invite.");
            var fields = new Dictionary<string, string> { ["email"] = email, ["role"] = role, ["accessUntil"] = until };
            if (project is not null)
            {
                await using var db = _web.CreateDbContext();
                fields["onlyProjectId"] = (await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync()).ToString();
            }
            _page = await WebScenario.ParseAsync(await PostAsync(admin, form.GetAttribute("action")!, page, fields));
        }

        private HttpClient Browser(string userName)
            => _browsers.TryGetValue(userName, out var browser) ? browser : _browsers[userName] = _web.CreateClient(signedInAs: userName);

        private async Task<IDocument> OpenAsync(string userName, string path)
            => await WebScenario.ParseAsync(await Browser(userName).GetAsync(path));

        private async Task<HttpResponseMessage> PostAsync(string userName, string path, IDocument page, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = page.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;
            return await Browser(userName).PostAsync(path, new FormUrlEncodedContent(fields));
        }

        [GeneratedRegex(@"/Invitations/Accept/[A-Za-z0-9_\-]+")]
        private static partial Regex LinkInMail();
    }
}
