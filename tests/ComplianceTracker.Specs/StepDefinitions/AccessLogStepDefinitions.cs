using System.Globalization;
using System.Net;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-219: the access log. UI contract: Views/AccessLog/Index.cshtml (table#access-log-visits with tr.access-visit
    // data-user, data-title, data-started, data-spent), Views/Shared/_LogPills.cshtml (#log-pills), _OnPageNow.cshtml
    // (#on-this-page li[data-user]), _AccessLogNotice.cshtml (#access-log-notice-starts), the layout's form#page-visit,
    // and Settings (form#turn-on-access-log, #access-log-state). Visits made earlier are written to the database as the
    // page's signs of life would make them, every 30 seconds while visible.
    [Binding]
    public class AccessLogStepDefinitions
    {
        private const string Organisation = "Acme ApS";
        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;

        public AccessLogStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        // ---------------------------------------------------------- Given

        // The admin turned it on 6 weeks before the day given (R7)
        [Given("the access log is on since {string}")]
        public async Task GivenTheAccessLogIsOnSince(string day)
        {
            var starts = DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            await using var db = _web.CreateDbContext();
            var organization = await db.Organizations.SingleAsync(o => o.Name == Organisation);
            organization.TurnOnAccessLog(starts, AccessLogRules.DefaultRetentionMonths, starts.AddDays(-AccessLogRules.NoticeDays));
            await db.SaveChangesAsync();
        }

        [Given("{string} opened the risk {string} at {string} and was last seen at {string}")]
        public async Task GivenOpenedTheRisk(string userName, string riskTitle, string from, string to)
        {
            await using var db = _web.CreateDbContext();
            var risk = await db.Risks.SingleAsync(r => r.Title == riskTitle);
            var project = await db.Projects.SingleAsync(p => p.Id == risk.ProjectId);
            await AddVisitAsync(db, userName, project.Id, VisitKind.Risk, risk.Id, "risks", $"Risk: {risk.Title} ({project.Name})",
                ClockStepDefinitions.Parse(from).UtcDateTime, ClockStepDefinitions.Parse(to).UtcDateTime);
        }

        [Given("{string} opened the risks of {string} at {string}")]
        public async Task GivenOpenedTheRisks(string userName, string projectName, string at)
        {
            await using var db = _web.CreateDbContext();
            var project = await db.Projects.SingleAsync(p => p.Name == projectName);
            var start = ClockStepDefinitions.Parse(at).UtcDateTime;
            await AddVisitAsync(db, userName, project.Id, VisitKind.Tab, null, "risks", $"{project.Name} – Risks", start, start.AddMinutes(1));
        }

        // R3: opened 3 minutes ago, the last sign of life 1 minute ago
        [Given("{string} has the risks of {string} open with a sign of life {int} minute(s) ago")]
        public async Task GivenHasTheRisksOpen(string userName, string projectName, int minutes)
        {
            await using var db = _web.CreateDbContext();
            var project = await db.Projects.SingleAsync(p => p.Name == projectName);
            var now = _clock.GetUtcNow().UtcDateTime;
            await AddVisitAsync(db, userName, project.Id, VisitKind.Tab, null, "risks", $"{project.Name} – Risks", now.AddMinutes(-minutes - 2), now.AddMinutes(-minutes));
        }

        // ---------------------------------------------------------- When

        [When("{string} opens the access log of {string}")]
        public async Task WhenOpensTheAccessLog(string userName, string projectName)
        {
            _web.SignInAs(userName);
            await _web.OpenAsync($"/AccessLog?project={await ProjectIdAsync(projectName)}");
        }

        [When("I open the risk {string} of {string}")]
        public async Task WhenIOpenTheRisk(string riskTitle, string projectName)
        {
            await using var db = _web.CreateDbContext();
            var risk = await db.Risks.SingleAsync(r => r.Title == riskTitle && r.ProjectId == db.Projects.Single(p => p.Name == projectName).Id);
            await _web.OpenAsync($"/Risks/Details/{risk.Id}");
        }

        // R2: as access-log.js sends it, with the form the layout gives the page
        [When("the page sends a sign of life")]
        public async Task WhenThePageSendsASignOfLife()
        {
            var form = _web.Page!.QuerySelector("form#page-visit") ?? throw new Xunit.Sdk.XunitException("The page has no visit.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["visitId"] = form.QuerySelector("input[name='visitId']")!.GetAttribute("value")!,
                ["visible"] = "true",
                ["ended"] = "false"
            });
            Assert.Equal(HttpStatusCode.NoContent, _web.Response!.StatusCode);
        }

        [When("I open the risks of {string}")]
        public async Task WhenIOpenTheRisks(string projectName)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(projectName)}/risks");

        // R6: as the daily clean-up runs it
        [When("the tracker cleans up on {string}")]
        public async Task WhenTheTrackerCleansUp(string at)
        {
            _clock.SetUtcNow(ClockStepDefinitions.Parse(at));
            using var scope = _web.App.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AccessLogService>().CleanUpAsync();
        }

        [When("I turn on the access log from {string} kept for {int} months")]
        public async Task WhenITurnOnTheAccessLog(string day, int months)
        {
            await _web.OpenAsync("/Settings");
            var form = _web.Page!.QuerySelector("form#turn-on-access-log") ?? throw new Xunit.Sdk.XunitException("Settings has no access log.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["startsOn"] = day,
                ["retentionMonths"] = months.ToString(CultureInfo.InvariantCulture)
            });
        }

        // ---------------------------------------------------------- Then

        [Then("the access log shows {string} opened {string} at {string} for {string}")]
        public void ThenTheAccessLogShows(string userName, string title, string at, string spent)
            => Assert.Contains(Visits(), v => v.User == userName && v.Title == title && v.Started == at && v.Spent == spent);

        [Then("the access log shows only {string}")]
        public void ThenTheAccessLogShowsOnly(string projectName)
        {
            var visits = Visits();
            Assert.NotEmpty(visits);
            Assert.All(visits, v => Assert.Contains(projectName, v.Title, StringComparison.Ordinal));
        }

        [Then("{string} has a visit to {string} of {int} seconds")]
        public async Task ThenHasAVisit(string userName, string title, int seconds)
        {
            await using var db = _web.CreateDbContext();
            var user = (await db.Organizations.SingleAsync(o => o.Name == Organisation)).Users.Single(u => u.Name == userName);
            var visit = await db.PageVisits.SingleAsync(v => v.UserId == user.Id && v.Title == title);
            Assert.Equal(seconds, visit.SecondsSpent);
            Assert.Equal(VisitKind.Risk, visit.Kind);
        }

        [Then("the log pages offer {string} only")]
        public void ThenTheLogPagesOfferOnly(string page)
            => Assert.Equal([page], _web.Page!.QuerySelectorAll("#log-pills a").Select(a => a.TextContent.Trim()));

        [Then("{string} may not open the access log")]
        public async Task ThenMayNotOpenTheAccessLog(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            var response = await client.GetAsync("/AccessLog");
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden
                || response.Headers.Location?.ToString().Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) == true,
                $"Expected access denied, got {(int)response.StatusCode}.");
        }

        [Then("I see {string} on the page now")]
        public void ThenISeeOnThePageNow(string userName)
            => Assert.Contains(_web.Page!.QuerySelectorAll("#on-this-page li.on-this-page"), li => li.GetAttribute("data-user") == userName);

        [Then("the access log has no visit from {string}")]
        public async Task ThenTheAccessLogHasNoVisitFrom(string day) => Assert.Equal(0, await VisitsOnAsync(day));

        [Then("the access log has a visit from {string}")]
        public async Task ThenTheAccessLogHasAVisitFrom(string day) => Assert.Equal(1, await VisitsOnAsync(day));

        [Then("the change log says the system deleted {int} visit(s) older than {int} months")]
        public async Task ThenTheChangeLogSays(int count, int months)
        {
            await using var db = _web.CreateDbContext();
            var organization = await db.Organizations.SingleAsync(o => o.Name == Organisation);
            var details = await db.ActivityLog
                .Where(e => e.OrganizationId == organization.Id && e.Action == ActivityAction.AccessLogCleanedUp && e.UserId == null)
                .Select(e => e.Details).ToListAsync();
            Assert.Equal([$"Deleted {count} {(count == 1 ? "visit" : "visits")} older than {months} months"], details);
        }

        [Then("the settings say {string}")]
        public void ThenTheSettingsSay(string text)
            => Assert.Contains(text, _web.Page!.Body!.TextContent, StringComparison.Ordinal);

        [Then("the access log starts on {string}")]
        public void ThenTheAccessLogStartsOn(string day)
            => Assert.Contains($"Starts on {day}", _web.Page!.QuerySelector("#access-log-state")?.TextContent ?? "", StringComparison.Ordinal);

        [Then("{string} is told on Home that the access log starts on {string}")]
        public async Task ThenIsToldOnHome(string userName, string day)
        {
            _web.SignInAs(userName);
            await _web.OpenAsync("/Review");
            Assert.Equal(day, _web.Page!.QuerySelector("#access-log-notice-starts")?.TextContent.Trim());
        }

        // ---------------------------------------------------------- Helpers

        private async Task AddVisitAsync(Infrastructure.Persistence.ComplianceTrackerDbContext db, string userName, Guid projectId, VisitKind kind,
            Guid? itemId, string tab, string title, DateTime from, DateTime to)
        {
            var organization = await db.Organizations.SingleAsync(o => o.Name == Organisation);
            var user = organization.Users.Single(u => u.Name == userName);
            var visit = PageVisit.Start(organization.Id, user.Id, user.Role, projectId, kind, itemId, tab, title, from);
            for (var at = from.Add(AccessLogRules.SignOfLifeEvery); at < to; at = at.Add(AccessLogRules.SignOfLifeEvery))
            {
                visit.SignOfLife(at, visible: true);
            }

            visit.SignOfLife(to, visible: true);
            db.PageVisits.Add(visit);
            await db.SaveChangesAsync();
        }

        private async Task<int> VisitsOnAsync(string day)
        {
            var date = DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var (from, to) = (date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
            await using var db = _web.CreateDbContext();
            return await db.PageVisits.CountAsync(v => v.StartedAt >= from && v.StartedAt <= to);
        }

        private List<(string User, string Title, string Started, string Spent)> Visits()
        {
            Assert.Equal(HttpStatusCode.OK, _web.Response!.StatusCode);
            return _web.Page!.QuerySelectorAll("#access-log-visits tr.access-visit")
                .Select(tr => (tr.GetAttribute("data-user")!, tr.GetAttribute("data-title")!, tr.GetAttribute("data-started")!, tr.GetAttribute("data-spent")!))
                .ToList();
        }

        private async Task<Guid> ProjectIdAsync(string projectName)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
        }
    }
}
