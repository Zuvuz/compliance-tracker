using ComplianceTracker.Domain.Users;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-217: the roles that approve accepting High and Critical risks, set on the Settings page (form#set-risk-approvers,
    // checkboxes name="roles"), by the organisation admin who is signed in. Roles are written as shown, e.g.
    // "Organisation admin" or "Compliance manager", several with "and".
    [Binding]
    public class RiskApproverStepDefinitions
    {
        private readonly WebScenario _web;

        public RiskApproverStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("only {string} approves accepting High and Critical risks")]
        [Given("only {string} approve accepting High and Critical risks")]
        [When("I let {string} approve accepting High and Critical risks")]
        public async Task GivenOnly(string roles)
        {
            await _web.OpenAsync("/Settings");
            Assert.NotNull(_web.Page!.QuerySelector("form#set-risk-approvers"));
            var fields = roles.Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(title => new KeyValuePair<string, string>("roles", RoleOf(title).ToString()))
                .ToList();
            await _web.PostFormAsync("/Settings/SetRiskApprovers", fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        [When("I try to let {string} approve accepting High and Critical risks")]
        public async Task WhenITry(string roles)
        {
            await _web.OpenAsync("/Settings");
            await _web.PostFormAsync("/Settings/SetRiskApprovers", new Dictionary<string, string> { ["roles"] = RoleOf(roles).ToString() });
        }

        [Then("the settings page says {string}")]
        public void ThenSettingsSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("nothing waits for approval in {string}")]
        public async Task ThenNothingWaits(string project)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.False(await db.ApprovalRequests.AnyAsync(r => r.ProjectId == projectId && r.Status == Domain.Approvals.ApprovalStatus.Pending));
        }

        // R3: no Approve button for the member, and the server refuses with the reason
        [Then("{string} cannot approve the request and is told {string}")]
        public async Task ThenCannotApprove(string userName, string message)
        {
            Domain.Approvals.ApprovalRequest request;
            await using (var db = _web.CreateDbContext())
            {
                request = await db.ApprovalRequests.Where(r => r.Status == Domain.Approvals.ApprovalStatus.Pending).OrderBy(r => r.RequestedAt).FirstAsync();
            }

            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{request.ProjectId}"));
            Assert.Null(page.QuerySelector($"li.approval[data-request='{request.Id}'] form.approve-request"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
            var answer = await WebScenario.ParseAsync(await client.PostAsync("/Approvals/Approve", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = request.Id.ToString(),
                ["projectId"] = request.ProjectId.ToString(),
                ["__RequestVerificationToken"] = token
            })));
            Assert.Contains(message, answer.Body!.TextContent, StringComparison.Ordinal);
            await using var check = _web.CreateDbContext();
            Assert.Equal(Domain.Approvals.ApprovalStatus.Pending, (await check.ApprovalRequests.SingleAsync(r => r.Id == request.Id)).Status);
        }

        private static UserRole RoleOf(string title)
            => Enum.GetValues<UserRole>().FirstOrDefault(r => string.Equals(RolePermissions.Title(r), title, StringComparison.OrdinalIgnoreCase)
                                                             || string.Equals(r.ToString(), title, StringComparison.OrdinalIgnoreCase),
                   (UserRole)(-1)) is var role && Enum.IsDefined(role)
                ? role
                : throw new Xunit.Sdk.XunitException($"No role '{title}'.");
    }
}
