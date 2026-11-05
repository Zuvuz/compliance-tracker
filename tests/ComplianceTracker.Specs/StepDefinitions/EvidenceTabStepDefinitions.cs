using AngleSharp.Dom;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-059: the Evidence tab (Views/Projects/_EvidenceTab.cshtml) and "Pick existing evidence" on the Compliance tab
    [Binding]
    public class EvidenceTabStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly FakeAi _ai;
        private readonly ScenarioClock _clock;
        private string? _migrationDatabase;
        private (Guid UserId, DateTime At)? _addedBefore;

        public EvidenceTabStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance, FakeAi ai, ScenarioClock clock)
        {
            _web = web;
            _compliance = compliance;
            _ai = ai;
            _clock = clock;
        }

        // ---------------------------------------------------------- Given

        [Given("the evidence {string} is on the Evidence tab of {string}")]
        public Task GivenTheEvidenceIsOnTheTab(string title, string project)
            => WhenIUploadTheEvidence(title, "access-policy.pdf", project);

        [Given("{string} has the suggestion {string}")]
        public async Task GivenHasTheSuggestion(string title, string code)
        {
            _ai.Rate(title, code, 0.8);
            await WhenIUploadTheEvidence(title, "access-policy.pdf", Project);
            Assert.NotNull(Suggestion(title, code));
        }

        [Given("the evidence {string} is linked to {string} and {string}")]
        public async Task GivenTheEvidenceIsLinked(string title, string first, string second)
        {
            await GivenTheEvidenceIsOnTheTab(title, Project);
            await WhenILinkToTheRequirements(title, first, second);
        }

        [Given("I reject the evidence suggestion {string} for {string} with the reason {string}")]
        public async Task GivenIRejectTheSuggestion(string code, string title, string reason)
        {
            var form = Suggestion(title, code)?.QuerySelector("form.reject-evidence-suggestion")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' has no suggestion '{code}' to reject.");
            var fields = HiddenFields(form);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // R10: a database of its own, at the migration before this story, with evidence on a requirement
        [Given("evidence {string} was added to {string} before the Evidence tab existed")]
        public async Task GivenEvidenceWasAddedBefore(string title, string code)
        {
            _migrationDatabase = $"evidence_migration_{Guid.NewGuid():N}";
            var main = new NpgsqlConnectionStringBuilder(_web.App.Factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["ConnectionStrings:ComplianceTracker"]);
            await using (var admin = new NpgsqlConnection(main.ConnectionString))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_migrationDatabase}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            var connection = MigrationConnectionString();
            await using (var db = TestDatabase.CreateContext(connection))
            {
                await db.Database.MigrateAsync();
            }

            // An organisation, a project and the evidence, made with today's model; then back to before this story
            await using (var services = TestServices.Create(_clock, new TestCurrentUser(TestUsers.Identity("Anna")), TestServices.Database(connection)))
            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<OrganizationService>()
                    .CreateAsync("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small);
            }

            await using (var db = TestDatabase.CreateContext(connection))
            {
                var organization = await db.Organizations.SingleAsync();
                var user = organization.Users.Single();
                var project = Domain.Projects.Project.Create(organization.Id, "Acme Portal", null, "A portal", null, Domain.Projects.LifecyclePhase.Development);
                db.Projects.Add(project);
                var requirement = await db.Requirements.Where(r => r.Code == code).OrderByDescending(r => r.Position).FirstAsync();
                var at = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);
                var evidence = Evidence.ForLink(project.Id, title, "https://github.com/acme/portal/pull/42", null, user.Id, UserRole.Admin, at);
                db.Evidence.Add(evidence);
                db.EvidenceLinks.Add(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, UserRole.Admin, at));
                await db.SaveChangesAsync();
                _addedBefore = (user.Id, at);

                var migrations = db.Database.GetMigrations().ToList();
                var before = migrations[migrations.FindIndex(m => m.EndsWith("_AddEvidenceLinks", StringComparison.Ordinal)) - 1];
                await db.GetService<IMigrator>().MigrateAsync(before);
            }
        }

        // ---------------------------------------------------------- When

        [When("I upload the evidence {string} with the file {string} on the Evidence tab of {string}")]
        public async Task WhenIUploadTheEvidence(string title, string fileName, string project)
        {
            await OpenTabAsync(project);
            var form = _web.Page!.QuerySelector("#evidence-upload")
                ?? throw new Xunit.Sdk.XunitException("The Evidence tab has no upload form.");
            var fields = HiddenFields(form);
            fields.Remove("__RequestVerificationToken"); // PostFileAsync adds it
            fields["title"] = title;
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, Encoding.ASCII.GetBytes($"%PDF-1.7\n% {title}\n"));
        }

        [When("I link {string} to the requirements {string} and {string}")]
        public async Task WhenILinkToTheRequirements(string title, string first, string second)
        {
            await OpenTabAsync(Project);
            var form = Item(title).QuerySelector("form.link-evidence")!;
            var ids = form.QuerySelectorAll("option")
                .Where(o => o.TextContent.StartsWith(first + " ", StringComparison.Ordinal) || o.TextContent.StartsWith(second + " ", StringComparison.Ordinal))
                .Select(o => o.GetAttribute("value")!)
                .ToList();
            Assert.Equal(2, ids.Count);

            var fields = HiddenFields(form).Select(f => new KeyValuePair<string, string>(f.Key, f.Value)).ToList();
            if (fields.All(f => f.Key != "__RequestVerificationToken"))
            {
                fields.Add(new("__RequestVerificationToken", _web.AntiforgeryToken()));
            }
            fields.AddRange(ids.Select(id => new KeyValuePair<string, string>("requirementIds", id)));
            _web.Response = await _web.Client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            var errors = _web.Page.QuerySelectorAll(".validation-summary-errors li").Select(e => e.TextContent.Trim()).ToList();
            Assert.True(_web.Response.IsSuccessStatusCode && errors.Count == 0, $"Linking gave: {string.Join(", ", errors)} ({(int)_web.Response.StatusCode} {_web.CurrentPath})");
        }

        [When("I approve the evidence suggestion {string} for {string}")]
        public async Task WhenIApproveTheSuggestion(string code, string title)
        {
            await OpenTabAsync(Project);
            var form = Suggestion(title, code)!.QuerySelector("form.approve-evidence-suggestion")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I ask for more evidence suggestions for {string}")]
        public async Task WhenIAskForMoreSuggestions(string title)
        {
            await OpenTabAsync(Project);
            var form = Item(title).QuerySelector("form.more-evidence-suggestions")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I remove the evidence {string} on the Evidence tab")]
        public async Task WhenIRemoveTheEvidenceOnTheTab(string title)
        {
            await OpenTabAsync(Project);
            var form = Item(title).QuerySelector("form.remove-project-evidence")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, HiddenFields(form));
        }

        [When("I pick the existing evidence {string} for {string}")]
        public async Task WhenIPickTheExistingEvidence(string title, string code)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            var form = Row(code).QuerySelector("form.pick-evidence")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' offers no existing evidence.");
            var fields = HiddenFields(form);
            fields["evidenceId"] = form.QuerySelectorAll("option").Single(o => o.TextContent.Trim() == title).GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("that database is migrated")]
        public async Task WhenThatDatabaseIsMigrated()
        {
            await using var db = TestDatabase.CreateContext(MigrationConnectionString());
            await db.Database.MigrateAsync();
        }

        [When("{string} opens the Evidence tab of {string}")]
        public async Task WhenOpensTheEvidenceTab(string user, string project)
        {
            using var client = _web.CreateClient(signedInAs: user);
            _web.Response = await client.GetAsync($"/Projects/Details/{await ProjectIdAsync(project)}/evidence");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        // ---------------------------------------------------------- Then

        [Then("the Evidence tab lists {string} with its SHA-256")]
        public async Task ThenTheTabListsWithItsSha256(string title)
        {
            await OpenTabAsync(Project);
            await using var db = _web.CreateDbContext();
            var sha = await db.Evidence.Where(e => e.Title == title).Select(e => e.Sha256).SingleAsync();
            Assert.Contains(sha!, Item(title).QuerySelector(".evidence-sha256")!.TextContent);
        }

        [Then("{string} has the suggestion {string} with score {string} marked {string}")]
        public async Task ThenHasTheSuggestionWithScore(string title, string code, string score, string mark)
        {
            await OpenTabAsync(Project);
            var suggestion = Suggestion(title, code) ?? throw new Xunit.Sdk.XunitException($"'{title}' has no suggestion '{code}'.");
            Assert.Equal(score, suggestion.QuerySelector(".suggestion-score")!.TextContent.Trim());
            Assert.Equal(mark, suggestion.QuerySelector(".ai-generated")!.TextContent.Trim());
        }

        [Then("{string} has no evidence suggestion {string}")]
        public async Task ThenHasNoSuggestion(string title, string code)
        {
            await OpenTabAsync(Project);
            Assert.Null(Suggestion(title, code));
        }

        [Then("the row {string} does not show the evidence {string}")]
        public void ThenTheRowDoesNotShowTheEvidence(string code, string title)
            => Assert.DoesNotContain(title, Row(code).QuerySelectorAll(".evidence").Select(e => e.GetAttribute("data-title")));

        [Then("the rows {string} and {string} no longer show {string}")]
        public void ThenTheRowsNoLongerShow(string first, string second, string title)
        {
            ThenTheRowDoesNotShowTheEvidence(first, title);
            ThenTheRowDoesNotShowTheEvidence(second, title);
        }

        [Then("evidence {string} is linked to {string} by the same person at the same time")]
        public async Task ThenEvidenceIsLinkedBySamePerson(string title, string code)
        {
            await using var db = TestDatabase.CreateContext(MigrationConnectionString());
            var evidence = await db.Evidence.SingleAsync(e => e.Title == title);
            var link = await db.EvidenceLinks.SingleAsync(l => l.EvidenceId == evidence.Id);
            var requirement = await db.Requirements.SingleAsync(r => r.Id == link.RequirementId);
            Assert.Equal(code, requirement.Code);
            Assert.Equal(_addedBefore!.Value.UserId, link.LinkedById);
            Assert.Equal(_addedBefore.Value.At, DateTime.SpecifyKind(link.LinkedAt, DateTimeKind.Utc));
            Assert.Equal(LinkSource.Manual, link.Source);
            Assert.True(link.IsActive);
        }

        [Then("{string} sees {string} linked to {string} and {string}")]
        public void ThenSeesLinkedTo(string user, string title, string first, string second)
            => Assert.Equal(
                new[] { first, second }.Order(),
                Item(title).QuerySelectorAll(".evidence-requirement").Select(r => r.GetAttribute("data-code")!).Order());

        // ---------------------------------------------------------- Helpers

        private async Task OpenTabAsync(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/evidence");

        private IElement Item(string title)
            => _web.Page!.QuerySelectorAll("#evidence-list .evidence-item").FirstOrDefault(e => e.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Evidence tab does not list '{title}' ({_web.CurrentPath}).");

        private IElement? Suggestion(string title, string code)
            => Item(title).QuerySelectorAll(".evidence-suggestion").FirstOrDefault(s => s.GetAttribute("data-code") == code);

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page lists no requirement '{code}'.");

        private static Dictionary<string, string> HiddenFields(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");

        private string MigrationConnectionString()
        {
            var builder = new NpgsqlConnectionStringBuilder(_web.App.Factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["ConnectionStrings:ComplianceTracker"])
            {
                Database = _migrationDatabase
            };
            return builder.ConnectionString;
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
