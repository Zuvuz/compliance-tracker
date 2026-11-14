using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-038: the evidence of a requirement row (.evidence[data-title]) with form.check-evidence, .evidence-check
    // (.evidence-check-verdict, .evidence-check-missing) and the forms .confirm-evidence-check and .overrule-evidence-check
    [Binding]
    public class EvidenceCheckStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private readonly FakeAi _ai;

        public EvidenceCheckStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance, FakeAi ai)
        {
            _web = web;
            _compliance = compliance;
            _ai = ai;
        }

        [Given("the AI finds that the evidence on {string} partly covers it and misses {string}")]
        public void GivenTheAiFindsPartly(string code, string missing)
            => _ai.EvidenceAnswers[code] = $"VERDICT: Partly covers\nMISSING: {missing}\nQUOTES: Users sign in with a second factor";

        [Given("I check the evidence {string} on {string} with AI")]
        [When("I check the evidence {string} on {string} with AI")]
        public Task WhenICheck(string title, string code) => PostAsync(title, code, "form.check-evidence");

        [When("I confirm the check of {string} on {string}")]
        public Task WhenIConfirm(string title, string code) => PostAsync(title, code, "form.confirm-evidence-check");

        [When("I overrule the check of {string} on {string} with {string} because {string}")]
        public Task WhenIOverrule(string title, string code, string verdict, string reason)
            => PostAsync(title, code, "form.overrule-evidence-check", new() { ["verdict"] = verdict, ["reason"] = reason });

        [Then("the evidence {string} on {string} shows {string} and {string}")]
        public async Task ThenShowsBoth(string title, string code, string first, string second)
        {
            var text = (await CheckAsync(title, code)).TextContent;
            Assert.Contains(first, text, StringComparison.Ordinal);
            Assert.Contains(second, text, StringComparison.Ordinal);
        }

        [Then("the evidence {string} on {string} shows {string}")]
        public async Task ThenShows(string title, string code, string text)
            => Assert.Equal(text, (await CheckAsync(title, code)).QuerySelector(".evidence-check-verdict")!.TextContent.Trim());

        [Then("the evidence {string} on {string} says it misses {string}")]
        public async Task ThenSaysItMisses(string title, string code, string missing)
            => Assert.Equal($"Missing: {missing}", (await CheckAsync(title, code)).QuerySelector(".evidence-check-missing")!.TextContent.Trim());

        [Then("{string} still has evidence")]
        public async Task ThenStillHasEvidence(string code) => Assert.Null((await RowAsync(code)).QuerySelector(".evidence-status"));

        [Then("{string} has no evidence any more")]
        public async Task ThenHasNoEvidence(string code)
            => Assert.Equal("No evidence", (await RowAsync(code)).QuerySelector(".evidence-status")?.TextContent.Trim());

        [Then("{string} has no evidence checks")]
        public async Task ThenHasNoChecks(string project)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(0, await db.EvidenceChecks.CountAsync());
        }

        [Then("the activity log has {string} with {string}")]
        public async Task ThenTheLogHasWith(string action, string text)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<Domain.ActivityLog.ActivityAction>(action);
            Assert.Contains(await db.ActivityLog.Where(e => e.Action == expected).Select(e => e.Details).ToListAsync(),
                d => d!.Contains(text, StringComparison.Ordinal));
        }

        private async Task PostAsync(string title, string code, string selector, Dictionary<string, string>? more = null)
        {
            var evidence = Evidence(await RowAsync(code), title);
            var form = evidence.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"'{title}' on '{code}' has no {selector}.");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            foreach (var (key, value) in more ?? [])
            {
                fields[key] = value;
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private async Task<IElement> CheckAsync(string title, string code)
            => Evidence(await RowAsync(code), title).QuerySelector(".evidence-check") ?? throw new Xunit.Sdk.XunitException($"'{title}' on '{code}' has no check.");

        private async Task<IElement> RowAsync(string code)
        {
            await _compliance.OpenSubTabWithAsync(Project, code);
            return RequirementRowLookup.Find(_web, code)
                ?? throw new Xunit.Sdk.XunitException($"No row '{code}'.");
        }

        private static IElement Evidence(IElement row, string title)
            => row.QuerySelectorAll(".evidence").FirstOrDefault(e => e.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The row has no evidence '{title}'.");
    }
}
