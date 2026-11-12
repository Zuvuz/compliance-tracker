using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-134: the Audits page (Views/Audits/Index.cshtml: form #record-audit, li.audit with .audit-corrected,
    // li.audit-finding[data-title] with .finding-state and form.close-finding) and the header's .certificate-badge.
    [Binding]
    public class AuditsStepDefinitions
    {
        private readonly WebScenario _web;

        public AuditsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I record an external audit of {string} for {string} by {string} with the certificate {string} valid to {string}")]
        [When("I record an external audit of {string} for {string} by {string} with the certificate {string} valid to {string}")]
        public Task WhenIRecordWithACertificate(string project, string scope, string company, string certificate, string validTo)
            => RecordAsync(project, Fields(scope, company, "2026-10-12", ("certificateNumber", certificate), ("certificateValidTo", validTo)));

        [Given("an audit of {string} found {string} for {string} due {string}")]
        public async Task GivenAnAuditFound(string project, string finding, string owner, string due)
        {
            await using var db = _web.CreateDbContext();
            var ownerId = (await db.Organizations.SingleAsync()).Users.Single(u => u.Name == owner).Id;
            await RecordAsync(project, Fields("ISO/IEC 27001:2022", "Internal team", "2026-11-01",
                ("findingTitle", finding), ("findingOwner", ownerId.ToString()), ("findingDue", due)));
        }

        [When("I record an audit of {string} held on {string}")]
        public Task WhenIRecordHeldOn(string project, string heldOn) => RecordAsync(project, Fields("ISO/IEC 27001:2022", "Bureau Veritas", heldOn));

        [When("I record an audit of {string} without the auditor's report")]
        public async Task WhenIRecordWithoutTheReport(string project)
        {
            var id = await OpenAsync(project);
            var fields = Fields("ISO/IEC 27001:2022", "Bureau Veritas", "2026-10-12");
            fields["projectId"] = id.ToString();
            await _web.PostFormAsync("/Audits/Record", fields);
        }

        [When("I record a correction of the audit of {string} with the certificate {string} valid to {string}")]
        public async Task WhenIRecordACorrection(string project, string certificate, string validTo)
        {
            await OpenAsync(project);
            var corrects = _web.Page!.QuerySelector("select[name=correctsAuditId] option:not([value=''])")!.GetAttribute("value")!;
            await RecordAsync(project, Fields("ISO/IEC 27001:2022", "Bureau Veritas", "2026-10-12",
                ("certificateNumber", certificate), ("certificateValidTo", validTo), ("correctsAuditId", corrects)));
        }

        [When("I close the finding {string} of {string} because {string}")]
        public async Task WhenICloseTheFinding(string finding, string project, string reason)
        {
            await OpenAsync(project);
            var form = Finding(finding).QuerySelector("form.close-finding")
                ?? throw new Xunit.Sdk.XunitException($"The finding '{finding}' cannot be closed.");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // The page offers the form only to those who may record; the request is sent anyway, so the server must refuse it
        [When("{string} records an audit of {string} anyway")]
        public async Task WhenRecordsAnyway(string userName, string project)
        {
            var id = await ProjectIdAsync(project);
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Audits?projectId={id}"));
            Assert.Null(page.QuerySelector("#record-audit"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value") ?? "";
            using var form = new MultipartFormDataContent();
            foreach (var (name, value) in Fields("ISO/IEC 27001:2022", "Bureau Veritas", "2026-10-12"))
            {
                form.Add(new StringContent(value), name);
            }

            form.Add(new StringContent(id.ToString()), "projectId");
            form.Add(new StringContent(token), "__RequestVerificationToken");
            form.Add(new ByteArrayContent(TestFiles.Pdf()), "file", "report.pdf");
            _web.Response = await client.PostAsync("/Audits/Record", form);
        }

        [Then("{string} shows the certificate {string}")]
        public async Task ThenShowsTheCertificate(string project, string badge)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}");
            Assert.Contains(badge, _web.Page!.QuerySelectorAll("#certificate-badges .certificate-badge").Select(b => b.TextContent.Trim()));
        }

        [Then("the finding {string} of {string} says {string}")]
        public async Task ThenTheFindingSays(string finding, string project, string state)
        {
            await OpenAsync(project);
            Assert.Equal(state, Finding(finding).QuerySelector(".finding-state")!.TextContent.Trim());
        }

        [Then("the first audit of {string} says it is corrected")]
        public async Task ThenTheFirstAuditIsCorrected(string project)
        {
            await OpenAsync(project);
            Assert.NotNull(_web.Page!.QuerySelectorAll("#audits li.audit").Last().QuerySelector(".audit-corrected"));
        }

        [Then("the project {string} has no audits")]
        public async Task ThenHasNoAudits(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await ProjectIdAsync(project);
            Assert.Equal(0, await db.Audits.CountAsync(a => a.ProjectId == id));
        }

        // ---------------------------------------------------------- Helpers

        private async Task RecordAsync(string project, Dictionary<string, string> fields)
        {
            var id = await OpenAsync(project);
            fields["projectId"] = id.ToString();
            await _web.PostFileAsync("/Audits/Record", fields, "audit-report.pdf", TestFiles.Pdf());
        }

        private static Dictionary<string, string> Fields(string scope, string company, string heldOn, params (string Name, string Value)[] more)
        {
            var fields = new Dictionary<string, string>
            {
                ["kind"] = "External",
                ["scope"] = scope,
                ["heldOn"] = heldOn,
                ["auditorName"] = "Lead auditor",
                ["auditorCompany"] = company,
                ["result"] = "Passed"
            };
            foreach (var (name, value) in more)
            {
                fields[name] = value;
            }

            return fields;
        }

        // "the organisation" is the organisation area, made the first time /Organisation is opened
        private async Task<Guid> OpenAsync(string project)
        {
            var id = await ProjectIdAsync(project);
            await _web.OpenAsync($"/Audits?projectId={id}");
            return id;
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            if (project == "the organisation")
            {
                await _web.OpenAsync("/Organisation");
            }

            await using var db = _web.CreateDbContext();
            return project == "the organisation"
                ? await db.Projects.Where(p => p.Kind == Domain.Projects.ProjectKind.Organisation).Select(p => p.Id).SingleAsync()
                : await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private IElement Finding(string title)
            => _web.Page!.QuerySelectorAll("li.audit-finding").FirstOrDefault(f => f.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Audits page has no finding '{title}'.");
    }
}
