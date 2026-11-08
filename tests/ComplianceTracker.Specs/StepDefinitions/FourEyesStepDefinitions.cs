using AngleSharp.Dom;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Xunit;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-135: confirmation of important actions and four-eyes approval
    [Binding]
    public class FourEyesStepDefinitions
    {
        private readonly WebScenario _web;

        public FourEyesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        // A member added straight in the database with the role; the invitation flow is covered by US-009
        [Given("{string} is in {string} with the role {string}")]
        public Task GivenIsAMemberWithTheRole(string userName, string organisation, string role)
            => FourEyes.AddMemberAsync(_web, userName, organisation, Enum.Parse<UserRole>(role));

        // ---------------------------------------------------------- When

        [Given("{string} approves the request")]
        [When("{string} approves the request")]
        public Task WhenApprovesTheRequest(string userName) => FourEyes.DecideAsync(_web, userName, "form.approve-request", null);

        // The page no longer offers an expired request; the approval is sent anyway, so the server must refuse it (R5)
        [When("{string} sends an approval of the request")]
        public async Task WhenSendsAnApproval(string userName)
        {
            var request = await FourEyes.PendingAsync(_web);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{request.ProjectId}"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
            _web.Response = await client.PostAsync("/Approvals/Approve", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = request.Id.ToString(),
                ["projectId"] = request.ProjectId.ToString(),
                ["__RequestVerificationToken"] = token
            }));
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        [When("{string} rejects the request because {string}")]
        public Task WhenRejectsTheRequest(string userName, string reason) => FourEyes.DecideAsync(_web, userName, "form.reject-request", reason);

        // The actions as a member sends them, without anyone approving (the other features' steps approve at once)
        [Given("I ask to accept the risk {string} with the reason {string}")]
        [When("I ask to accept the risk {string} with the reason {string}")]
        public async Task WhenIAskToAcceptTheRisk(string title, string reason)
        {
            await _web.OpenAsync(await FourEyes.PathAsync(_web, $"the risk page of {title}"));
            var form = _web.Page!.QuerySelector("#accept-risk") ?? throw new Xunit.Sdk.XunitException("No accept form.");
            var fields = FourEyes.Hidden(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I ask to record the vulnerability {string}, not exploited, for {string}")]
        public async Task WhenIAskToRecordNotExploited(string title, string projectName)
        {
            await _web.OpenAsync(await FourEyes.PathAsync(_web, $"the Security events tab of {projectName}"));
            var form = _web.Page!.QuerySelector("#record-security-event") ?? throw new Xunit.Sdk.XunitException("No record form.");
            var fields = FourEyes.Hidden(form);
            fields["kind"] = "VulnerabilityNotExploited";
            fields["title"] = title;
            fields["awareAt"] = DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I ask to end the support of {string}")]
        public async Task WhenIAskToEndTheSupport(string projectName)
        {
            var id = await FourEyes.ProjectIdAsync(_web, projectName);
            await _web.OpenAsync($"/Projects/Details/{id}");
            await _web.PostFormAsync("/Projects/ChangePhase", new Dictionary<string, string>
            {
                ["projectId"] = id.ToString(),
                ["phase"] = "EndOfSupport"
            });
        }

        [When("I approve my own request")]
        public async Task WhenIApproveMyOwnRequest()
        {
            // The page hides the button (R3); the request is sent anyway, so the server must refuse it
            var request = await FourEyes.PendingAsync(_web);
            await _web.OpenAsync($"/Projects/Details/{request.ProjectId}");
            Assert.Null(_web.Page!.QuerySelector("form.approve-request"));
            await _web.PostFormAsync("/Approvals/Approve", new Dictionary<string, string>
            {
                ["id"] = request.Id.ToString(),
                ["projectId"] = request.ProjectId.ToString()
            });
        }

        [When("I withdraw the request")]
        public async Task WhenIWithdrawTheRequest()
        {
            var request = await FourEyes.PendingAsync(_web);
            await _web.OpenAsync($"/Projects/Details/{request.ProjectId}");
            var form = _web.Page!.QuerySelector("form.withdraw-request") ?? throw new Xunit.Sdk.XunitException("No withdraw button.");
            await _web.PostFormAsync(form.GetAttribute("action")!, FourEyes.Hidden(form));
        }

        [Given("the request is {int} days old")]
        public async Task WhenTheRequestIsDaysOld(int days)
        {
            await using var db = _web.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"ApprovalRequests\" SET \"RequestedAt\" = \"RequestedAt\" - make_interval(days => {0}) WHERE \"Status\" = 'Pending'", days);
        }

        // ---------------------------------------------------------- Then

        [Then("I see that it waits for another member's approval")]
        public void ThenISeeThatItWaits()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#approval-requested"));
        }

        [Then("{string} of {string} waits for approval")]
        public async Task ThenWaitsForApproval(string text, string projectName)
        {
            var id = await FourEyes.ProjectIdAsync(_web, projectName);
            await _web.OpenAsync($"/Projects/Details/{id}");
            var waiting = _web.Page!.QuerySelectorAll("#approvals li.approval .approval-summary").Select(e => e.TextContent.Trim()).ToList();
            Assert.Contains(waiting, w => w.Contains(text, StringComparison.Ordinal));
        }

        [Then("{string} sees {string} under \"Needs attention\" of {string}")]
        public async Task ThenSeesUnderNeedsAttention(string userName, string text, string projectName)
        {
            var id = await FourEyes.ProjectIdAsync(_web, projectName);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{id}"));
            var items = page.QuerySelectorAll("#needs-attention li").Select(e => e.TextContent.Trim()).ToList();
            Assert.Contains(items, i => i.Contains(text, StringComparison.Ordinal));
        }

        [Then("{string} cannot approve the request")]
        public async Task ThenCannotApprove(string userName)
        {
            var request = await FourEyes.PendingAsync(_web);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{request.ProjectId}"));
            Assert.Null(page.QuerySelector($"li.approval[data-request='{request.Id}'] form.approve-request"));

            // R3: the server refuses it too
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
            await client.PostAsync("/Approvals/Approve", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = request.Id.ToString(),
                ["projectId"] = request.ProjectId.ToString(),
                ["__RequestVerificationToken"] = token
            }));
            await using var db = _web.CreateDbContext();
            Assert.Equal(ApprovalStatus.Pending, (await db.ApprovalRequests.SingleAsync(r => r.Id == request.Id)).Status);
        }

        [Then("the request has the status {string}")]
        public async Task ThenTheRequestHasTheStatus(string status)
        {
            await using var db = _web.CreateDbContext();
            var request = await db.ApprovalRequests.OrderByDescending(r => r.RequestedAt).FirstAsync();
            Assert.Equal(Enum.Parse<ApprovalStatus>(status), request.Status);
        }

        [Then("the page says {string}")]
        public void ThenThePageSays(string text)
        {
            Assert.Contains(text, _web.Page!.Body!.TextContent, StringComparison.Ordinal);
        }

        [Then("the {string} form on {string} asks for confirmation")]
        public async Task ThenTheFormAsksForConfirmation(string selector, string path)
        {
            await _web.OpenAsync(await FourEyes.PathAsync(_web, path));
            var form = _web.Page!.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"No form '{selector}' on {path}.");
            Assert.False(string.IsNullOrWhiteSpace(form.GetAttribute("data-confirm")));
        }

        [Then("the page has the confirmation dialog")]
        public void ThenThePageHasTheConfirmationDialog()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#confirm-dialog #confirm-dialog-ok"));
        }
    }

    // US-135: shared by the steps of other features whose Given-steps need an action that waits for approval
    public static class FourEyes
    {
        // The member who approves in the Given-steps of other features
        public const string Approver = "Approver";

        public static async Task AddMemberAsync(WebScenario web, string userName, string organisation, UserRole role)
        {
            await using var db = web.CreateDbContext();
            var org = await db.Organizations.Include(o => o.Users).SingleAsync(o => o.Name == organisation);
            if (org.Users.Any(u => u.Name == userName))
            {
                return;
            }

            org.AddMember(TestUsers.Identity(userName), role);
            await db.SaveChangesAsync();
        }

        // For Given-steps such as "X is in phase EndOfSupport": a second member approves what waits
        public static async Task ApproveAllAsync(WebScenario web)
        {
            await using (var db = web.CreateDbContext())
            {
                if (!await db.ApprovalRequests.AnyAsync(r => r.Status == ApprovalStatus.Pending))
                {
                    return;
                }

                var organisation = await db.Organizations.Where(o => o.Id == db.ApprovalRequests.First(r => r.Status == ApprovalStatus.Pending).OrganizationId)
                    .Select(o => o.Name).SingleAsync();
                await AddMemberAsync(web, Approver, organisation, UserRole.Admin);
            }

            while (await PendingOrNullAsync(web) is not null)
            {
                await DecideAsync(web, Approver, "form.approve-request", null);
            }
        }

        public static async Task DecideAsync(WebScenario web, string userName, string formSelector, string? reason)
        {
            var request = await PendingAsync(web);
            using var client = web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{request.ProjectId}"));
            var form = page.QuerySelector($"li.approval[data-request='{request.Id}'] {formSelector}")
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' cannot decide the request: {page.QuerySelector("#needs-attention")?.TextContent.Trim()}");
            var fields = Hidden(form);
            if (reason is not null)
            {
                fields["reason"] = reason;
            }

            web.Response = await client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            web.Page = await WebScenario.ParseAsync(web.Response);
        }

        public static async Task<ApprovalRequest> PendingAsync(WebScenario web)
            => await PendingOrNullAsync(web) ?? throw new Xunit.Sdk.XunitException("Nothing waits for approval.");

        private static async Task<ApprovalRequest?> PendingOrNullAsync(WebScenario web)
        {
            await using var db = web.CreateDbContext();
            return await db.ApprovalRequests.Where(r => r.Status == ApprovalStatus.Pending).OrderBy(r => r.RequestedAt).FirstOrDefaultAsync();
        }

        public static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        public static async Task<Guid> ProjectIdAsync(WebScenario web, string projectName)
        {
            await using var db = web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }

        // "the risk page of Old TLS", "the Overview of Acme Portal", or a plain path
        public static async Task<string> PathAsync(WebScenario web, string where)
        {
            await using var db = web.CreateDbContext();
            if (where.StartsWith("the risk page of ", StringComparison.Ordinal))
            {
                var title = where["the risk page of ".Length..];
                return $"/Risks/Details/{await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync()}";
            }

            foreach (var (prefix, tab) in new[] { ("the Overview of ", "overview"), ("the Scans tab of ", "scans"), ("the Security events tab of ", "security-events") })
            {
                if (where.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var name = where[prefix.Length..];
                    return $"/Projects/Details/{await db.Projects.Where(p => p.Name == name).Select(p => p.Id).SingleAsync()}?tab={tab}";
                }
            }

            return where;
        }
    }
}
