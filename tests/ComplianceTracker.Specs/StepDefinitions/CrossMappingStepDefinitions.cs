using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-037: the Cross-mapping page, through the web app like a user.
    // UI contract: Views/Frameworks/CrossMapping.cshtml (form#choose-pair with the selects from and to,
    // form#suggest-cross-mapping, #cross-mapping-message, #pending-matches li.match[data-from][data-to] with
    // .match-strength and the forms .approve-match and .reject-match, #approved-matches li.match); the Compliance tab's
    // tr.requirement-row[data-code] with .requirement-status and .covered-through.
    [Binding]
    public class CrossMappingStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly ComplianceTabStepDefinitions _compliance;
        private string _pairUrl = "/Frameworks/CrossMapping";

        public CrossMappingStepDefinitions(WebScenario web, ComplianceTabStepDefinitions compliance)
        {
            _web = web;
            _compliance = compliance;
        }

        // Chooses the pair in the two selects, like a user, then clicks "Suggest matches"
        [Given("I ask the AI to match {string} with {string}")]
        [When("I ask the AI to match {string} with {string}")]
        public async Task WhenIAskTheAiToMatch(string from, string to)
        {
            await _web.OpenAsync("/Frameworks");
            await _web.OpenAsync(_web.Page!.QuerySelector("#cross-mapping-link")!.GetAttribute("href")!);
            _pairUrl = $"/Frameworks/CrossMapping?from={Option("from", from)}&to={Option("to", to)}";
            await _web.OpenAsync(_pairUrl);
            await SubmitAsync(_web.Page!.QuerySelector("form#suggest-cross-mapping")!);
        }

        [When("I approve the cross-mapping of {string} and {string}")]
        public async Task WhenIApprove(string from, string to)
            => await SubmitAsync((await PendingAsync(from, to)).QuerySelector("form.approve-match")!);

        [When("I reject the cross-mapping of {string} and {string} without a reason")]
        public async Task WhenIRejectWithoutReason(string from, string to)
            => await SubmitAsync((await PendingAsync(from, to)).QuerySelector("form.reject-match")!);

        [When("I reject the cross-mapping of {string} and {string} because {string}")]
        public async Task WhenIReject(string from, string to, string reason)
            => await SubmitAsync((await PendingAsync(from, to)).QuerySelector("form.reject-match")!, new() { ["reason"] = reason });

        [Then("the cross-mapping suggests {string} and {string} as {string}")]
        public async Task ThenSuggests(string from, string to, string strength)
            => Assert.Equal(strength, (await PendingAsync(from, to)).QuerySelector(".match-strength")!.TextContent.Trim());

        [Then("every cross-mapping suggestion is marked {string}")]
        public async Task ThenEverySuggestionIsMarked(string label)
        {
            await _web.OpenAsync(_pairUrl);
            var rows = _web.Page!.QuerySelectorAll("#pending-matches li.match");
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Contains(label, row.TextContent, StringComparison.Ordinal));
        }

        [Then("the cross-mapping has no suggestions")]
        public async Task ThenNoSuggestions()
        {
            await _web.OpenAsync(_pairUrl);
            Assert.Empty(_web.Page!.QuerySelectorAll("#pending-matches li.match"));
        }

        [Then("the cross-mapping page says {string}")]
        public void ThenThePageSays(string message)
            => Assert.Equal(message, _web.Page!.QuerySelector("#cross-mapping-message")?.TextContent.Trim());

        [Then("the approved matches list {string} and {string}")]
        public async Task ThenApprovedMatchesList(string from, string to)
        {
            await _web.OpenAsync(_pairUrl);
            Assert.Single(_web.Page!.QuerySelectorAll($"#approved-matches li.match[data-from='{from}'][data-to='{to}']"));
            Assert.Empty(_web.Page!.QuerySelectorAll("#pending-matches li.match"));
        }

        [Then("the requirement {string} of {string} is covered through {string}")]
        public async Task ThenCoveredThrough(string code, string project, string through)
        {
            var row = await RowAsync(code, project);
            Assert.Equal("Covered", row.QuerySelector(".requirement-status")?.TextContent.Trim());
            Assert.Equal($"through {through}", row.QuerySelector(".covered-through")?.TextContent.Trim());
        }

        [Then("the requirement {string} of {string} is not covered")]
        public async Task ThenNotCovered(string code, string project)
        {
            var row = await RowAsync(code, project);
            Assert.NotEqual("Covered", row.QuerySelector(".requirement-status")?.TextContent.Trim());
            Assert.Null(row.QuerySelector(".covered-through"));
        }

        // Sends the form's hidden fields (and any extra ones) to its action, like clicking its button
        private async Task SubmitAsync(IElement form, Dictionary<string, string>? more = null)
        {
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            foreach (var (key, value) in more ?? [])
            {
                fields[key] = value;
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // The version id of the option whose label starts with the framework's short name
        private string Option(string select, string framework)
            => _web.Page!.QuerySelectorAll($"form#choose-pair select[name='{select}'] option")
                   .FirstOrDefault(o => o.TextContent.Trim().StartsWith(framework + " v", StringComparison.Ordinal))?.GetAttribute("value")
               ?? throw new Xunit.Sdk.XunitException($"The Cross-mapping page has no framework '{framework}'.");

        private async Task<IElement> PendingAsync(string from, string to)
        {
            await _web.OpenAsync(_pairUrl);
            return _web.Page!.QuerySelector($"#pending-matches li.match[data-from='{from}'][data-to='{to}']")
                ?? throw new Xunit.Sdk.XunitException($"No pending cross-mapping of '{from}' and '{to}'.");
        }

        private async Task<IElement> RowAsync(string code, string project)
        {
            await _compliance.OpenSubTabWithAsync(project, code);
            return RequirementRowLookup.Find(_web, code)!;
        }
    }
}
