using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-098: the log lines the app wrote, and the error page (#error-reference)
    [Binding]
    public class OperationalLogsStepDefinitions
    {
        private readonly WebScenario _web;
        private CapturedLogs.Entry? _entry;

        public OperationalLogsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the error page")]
        public Task WhenIOpenTheErrorPage() => _web.OpenAsync("/Home/Error");

        [Then("the log has the security event {string}")]
        public void ThenTheLogHasTheSecurityEvent(string text)
        {
            _entry = _web.Logs.Entries.LastOrDefault(e => e.Category == "ComplianceTracker.Security" && e.Message.Contains(text, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"No security event '{text}'; the security log has: {string.Join(" | ", _web.Logs.Entries.Where(e => e.Category == "ComplianceTracker.Security").Select(e => e.Message))}");
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, _entry.Level);
        }

        [Then("that log line has a reference and the user id of {string}, but not the name or e-mail")]
        public async Task ThenThatLineHasTheReferenceAndUser(string userName)
        {
            await using var db = _web.CreateDbContext();
            var user = (await db.Organizations.SingleAsync()).Users.Single(u => u.Name == userName);
            Assert.False(string.IsNullOrEmpty(_entry!.Scope["Reference"]?.ToString()));
            Assert.Equal(user.ExternalId, _entry.Scope["UserId"]?.ToString());
            var all = _entry.Message + string.Join(" ", _entry.Scope.Values);
            Assert.DoesNotContain(user.Name, all, StringComparison.Ordinal);
            Assert.DoesNotContain(user.Email, all, StringComparison.OrdinalIgnoreCase);
        }

        [Then("the error page shows a reference")]
        public void ThenTheErrorPageShowsAReference()
            => Assert.False(string.IsNullOrWhiteSpace(_web.Page!.QuerySelector("#error-reference")?.TextContent));

        [Then("the error page says {string}")]
        public void ThenTheErrorPageSays(string text) => Assert.Contains(text, _web.Page!.QuerySelector("h1")!.TextContent);
    }
}
