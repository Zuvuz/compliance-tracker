using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-211: the internal audit programme (/Isms/InternalAudits). UI contract: Views/Isms/InternalAudits.cshtml
    // (form#audit-cycle, #audit-coverage rows with data-item and data-state, #audit-missing, form#control-owner,
    // form#plan-audit, #planned-audits rows with data-planned and data-done (.planned-overdue), form.link-recorded-audit,
    // #internal-audits-read-only). The nonconformity steps of US-212 check the findings.
    [Binding]
    public class AuditProgrammeStepDefinitions
    {
        private const string Path = "/Isms/InternalAudits";
        private readonly WebScenario _web;

        public AuditProgrammeStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("an audit programme of {int} years from {string}")]
        public async Task GivenAProgramme(int years, string start)
        {
            await WhenIStart(years, start);
            AssertNoError();
        }

        [When("I start an audit programme of {int} years from {string}")]
        public async Task WhenIStart(int years, string start)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/AuditCycle", new Dictionary<string, string> { ["startsOn"] = start, ["years"] = years.ToString() });
        }

        [Given("{string} owns the control {string}")]
        public async Task GivenOwns(string name, string control)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/ControlOwner", new Dictionary<string, string> { ["code"] = control, ["ownerId"] = (await MemberAsync(name)).ToString() });
            AssertNoError();
        }

        [Given("I plan an audit on {string} for the clauses {string} and {string}")]
        [When("I plan an audit on {string} for the clauses {string} and {string}")]
        public async Task WhenIPlanClauses(string on, string first, string second)
        {
            await PlanAsync(on, [new("clauses", first), new("clauses", second)], "Anna", null);
            AssertNoError();
        }

        [When("I plan an audit of {string} with {string} as auditor")]
        public Task WhenIPlanControl(string control, string auditor) => PlanAsync("2027-03-01", [new("controls", control)], auditor, null);

        [When("I plan an audit of {string} with {string} as auditor because {string}")]
        public Task WhenIPlanControlWithReason(string control, string auditor, string reason)
            => PlanAsync("2027-03-01", [new("controls", control)], auditor, reason);

        [When("I mark the audit planned on {string} as done with the recorded audit")]
        public async Task WhenIMarkDone(string on)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector($"#planned-audits tr[data-planned='{on}'] form.link-recorded-audit")
                       ?? throw new Xunit.Sdk.XunitException($"The audit planned on {on} cannot be marked done.");
            var auditId = form.QuerySelector("select[name=auditId] option")!.GetAttribute("value")!;
            var plannedId = form.QuerySelector("input[name=plannedAuditId]")!.GetAttribute("value")!;
            await _web.PostFormAsync("/Isms/LinkRecordedAudit", new Dictionary<string, string> { ["plannedAuditId"] = plannedId, ["auditId"] = auditId });
            AssertNoError();
        }

        [When("I open the internal audits")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("the coverage shows the clause {string} as {word}")]
        [Then("the coverage shows the control {string} as {word}")]
        public async Task ThenCoverage(string item, string state)
        {
            await _web.OpenAsync(Path);
            var row = _web.Page!.QuerySelector($"#audit-coverage tr[data-item='{item}']") ?? throw new Xunit.Sdk.XunitException($"No '{item}' in the coverage.");
            Assert.Equal(state, row.GetAttribute("data-state"), ignoreCase: true);
        }

        [Then("the audit coverage says {string}")]
        public void ThenMissing(string text) => Assert.Contains(text, _web.Page!.QuerySelector("#audit-missing")?.TextContent ?? "", StringComparison.Ordinal);

        [Then("the audit programme warns {string}")]
        public void ThenWarned(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("nothing is planned")]
        public async Task ThenNothingPlanned()
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.AuditProgrammes.SelectMany(p => p.Audits).AnyAsync());
        }

        [Then("the audit planned on {string} is overdue")]
        public void ThenOverdue(string on)
            => Assert.NotNull(_web.Page!.QuerySelector($"#planned-audits tr[data-planned='{on}'] .planned-overdue"));

        [Then("the internal audits are read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#internal-audits-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#audit-cycle, form#plan-audit, form#control-owner, form.link-recorded-audit"));
        }

        private async Task PlanAsync(string on, List<KeyValuePair<string, string>> items, string auditor, string? reason)
        {
            await _web.OpenAsync(Path);
            var fields = new List<KeyValuePair<string, string>> { new("on", on), new("auditorId", (await MemberAsync(auditor)).ToString()) };
            fields.AddRange(items);
            if (reason is not null)
            {
                fields.Add(new("reason", reason));
            }

            await _web.PostFormAsync("/Isms/PlanAudit", fields);
        }

        private async Task<Guid> MemberAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == name).Select(u => u.Id).FirstAsync();
        }

        private void AssertNoError() => Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
    }
}
