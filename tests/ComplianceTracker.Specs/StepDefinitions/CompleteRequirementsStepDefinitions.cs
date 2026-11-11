using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-137: imports from the pretend EUR-Lex and the completeness check, through the Frameworks pages like a user.
    // UI contract: Views/Frameworks/Index.cshtml (#import-eur-lex), Views/Frameworks/Version.cshtml (#completeness,
    // #profile-check, #pending-activation with form.approve-request, #version-status).
    [Binding]
    public class CompleteRequirementsStepDefinitions
    {
        private readonly WebScenario _web;
        private string? _versionId;

        public CompleteRequirementsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I have imported {string} with the CELEX number {string} from EUR-Lex")]
        [When("I import {string} with the CELEX number {string} from EUR-Lex")]
        public async Task WhenIImportFromEurLex(string name, string celex)
        {
            await _web.OpenAsync("/Frameworks");
            var form = _web.Page!.QuerySelector("#import-eur-lex form") ?? throw new Xunit.Sdk.XunitException("The page offers no import from EUR-Lex.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["name"] = name, ["celex"] = celex, ["appliesFrom"] = "" });
            _versionId = _web.CurrentPath.StartsWith("/Frameworks/Version/", StringComparison.OrdinalIgnoreCase) ? FrameworkUi.OpenVersionId(_web) : null;
        }

        [When("I upload {string} as a PDF without the control {string}")]
        public async Task WhenIUploadWithoutTheControl(string name, string control)
        {
            await _web.OpenAsync("/Frameworks");
            await _web.PostFileAsync("/Frameworks/Add", new Dictionary<string, string> { ["Name"] = name }, "iso.pdf", StandardFiles.ManagementSystemPdf(without: control));
            _versionId = FrameworkUi.OpenVersionId(_web);
        }

        [When("I activate the draft version")]
        public Task WhenIActivateTheDraftVersion() => FrameworkUi.ActivateAsync(_web, _versionId!);

        [When("{string} approves the activation")]
        public async Task WhenApprovesTheActivation(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Frameworks/Version/{_versionId}"));
            var form = page.QuerySelector("#pending-activation form.approve-request")
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' is not offered to approve the activation.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            _web.Response = await client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            await FrameworkUi.OpenVersionAsync(_web, _versionId!);
        }

        // ---------------------------------------------------------- Then

        [Then("the draft version has the requirement {string}")]
        public async Task ThenTheDraftHasTheRequirement(string code) => Assert.Contains(code, await CodesAsync());

        [Then("the draft version has no requirement {string}")]
        public async Task ThenTheDraftHasNoRequirement(string code) => Assert.DoesNotContain(code, await CodesAsync());

        [Then("the recitals are left out with the reason {string}")]
        public async Task ThenTheRecitalsAreLeftOut(string reason)
        {
            await FrameworkUi.OpenVersionAsync(_web, _versionId!);
            var parts = LeftOut();
            Assert.Contains(parts, p => p.Code.StartsWith("Recital", StringComparison.Ordinal) && p.Reason == reason);
            Assert.All(parts.Where(p => p.Code.StartsWith("Recital", StringComparison.Ordinal)), p => Assert.Equal(reason, p.Reason));
        }

        [Then("{string} is left out with the reason {string}")]
        public void ThenIsLeftOutWithTheReason(string code, string reason)
            => Assert.Contains((code, reason), LeftOut().Select(p => (p.Code, p.Reason)));

        [Then("the completeness check shows {string}")]
        [Then("it shows {string}")]
        public void ThenTheCompletenessCheckShows(string text)
        {
            var check = _web.Page!.QuerySelector("#completeness") ?? throw new Xunit.Sdk.XunitException("The draft shows no completeness check.");
            Assert.Contains(text, check.TextContent.Split('\n').Select(l => l.Trim()));
        }

        [Then("the draft version waits for approval")]
        public async Task ThenTheDraftWaitsForApproval()
        {
            await FrameworkUi.OpenVersionAsync(_web, _versionId!);
            Assert.Equal("Waiting for approval", _web.Page!.QuerySelector("#version-status")?.TextContent.Trim());
            await using var db = _web.CreateDbContext();
            Assert.Equal(FrameworkVersionStatus.Draft, await db.FrameworkVersions.Where(v => v.Id == Guid.Parse(_versionId!)).Select(v => v.Status).SingleAsync());
        }

        [Then("the version is active")]
        public void ThenTheVersionIsActive() => Assert.Equal("Active", _web.Page!.QuerySelector("#version-status")?.TextContent.Trim());

        [Then("I am not offered to approve it")]
        public async Task ThenIAmNotOfferedToApproveIt()
        {
            await FrameworkUi.OpenVersionAsync(_web, _versionId!);
            Assert.NotNull(_web.Page!.QuerySelector("#pending-activation form.withdraw-request"));
            Assert.Null(_web.Page!.QuerySelector("#pending-activation form.approve-request"));
        }

        // ---------------------------------------------------------- Helpers

        // "Recital (1) – Recital" → ("Recital (1)", "Recital")
        private List<(string Code, string Reason)> LeftOut()
        {
            if (_web.Page!.QuerySelector("#left-out-parts") is null)
            {
                throw new Xunit.Sdk.XunitException("The draft lists nothing that was left out.");
            }

            return _web.Page!.QuerySelectorAll("li.left-out-part")
                .Select(li => (li.TextContent.Split(" – ")[0].Trim(), li.GetAttribute("data-reason") ?? ""))
                .ToList();
        }

        private async Task<List<string>> CodesAsync()
        {
            Assert.NotNull(_versionId);
            await using var db = _web.CreateDbContext();
            var id = Guid.Parse(_versionId!);
            return await db.Requirements.Where(r => r.FrameworkVersionId == id).Select(r => r.Code).ToListAsync();
        }
    }
}
