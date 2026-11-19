using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-206: the Cross-mapping page for several frameworks (Views/Frameworks/CrossMappingSet.cshtml): form#choose-frameworks
    // with checkboxes v, form#suggest-several, #match-groups li.match-group[data-code] with li.group-match[data-code]
    // [data-framework] and the forms .approve-group and .reject-group, and #approved-matches li.approved-match[data-from]
    // [data-to] with .match-applies-to and form.change-match-projects (projectIds).
    [Binding]
    public class CrossMappingSeveralStepDefinitions
    {
        private readonly WebScenario _web;
        private string _setUrl = "/Frameworks/CrossMapping";

        public CrossMappingSeveralStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // Ticks the frameworks in the order named, like a user, then clicks "Suggest matches"
        [Given("I ask the AI to match {string}, {string} and {string} at once")]
        [When("I ask the AI to match {string}, {string} and {string} at once")]
        public async Task WhenIAskForSeveral(string first, string second, string third)
        {
            await OpenSeveralAsync();
            _setUrl = SetUrl([first, second, third]);
            await _web.OpenAsync(_setUrl);
            await SubmitAsync(_web.Page!.QuerySelector("form#suggest-several") ?? throw new Xunit.Sdk.XunitException("No \"Suggest matches\" for several frameworks."));
        }

        [When("I approve all matches of {string}")]
        public async Task WhenIApproveAll(string code)
            => await SubmitAsync((await GroupAsync(code)).QuerySelector("form.approve-group")!);

        [When("I limit the match of {string} and {string} to {string}")]
        public async Task WhenILimit(string from, string to, string project)
        {
            // The pair page links to the page for several frameworks, where the projects are chosen
            await OpenSeveralAsync();
            var form = Approved(from, to).QuerySelector("form.change-match-projects") ?? throw new Xunit.Sdk.XunitException("The match's projects cannot be changed.");
            var box = form.QuerySelectorAll("input[name='projectIds']").FirstOrDefault(i => i.ParentElement!.TextContent.Trim() == project)
                ?? throw new Xunit.Sdk.XunitException($"'{project}' cannot be chosen for the match.");
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .Select(i => new KeyValuePair<string, string>(i.GetAttribute("name")!, i.GetAttribute("value") ?? ""))
                .Append(new KeyValuePair<string, string>("projectIds", box.GetAttribute("value")!))
                .ToList();
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the matches of {string} are {string}")]
        public async Task ThenTheMatchesAre(string code, string matches)
        {
            var group = await GroupAsync(code);
            Assert.Equal(matches, string.Join(", ", group.QuerySelectorAll("li.group-match").Select(m => $"{m.GetAttribute("data-framework")} {m.GetAttribute("data-code")}")));
        }

        [Then("the approved matches across the frameworks are {string}")]
        public async Task ThenTheApprovedAre(string matches)
        {
            await _web.OpenAsync(_setUrl);
            Assert.Equal(matches, string.Join(", ", _web.Page!.QuerySelectorAll("#approved-matches li.approved-match")
                .Select(m => $"{m.GetAttribute("data-from")} ↔ {m.GetAttribute("data-to")}")));
        }

        [Then("nothing waits for a decision across the frameworks")]
        public async Task ThenNothingWaits()
        {
            await _web.OpenAsync(_setUrl);
            Assert.Empty(_web.Page!.QuerySelectorAll("#match-groups li.match-group"));
        }

        [Then("the match of {string} and {string} applies to {string}")]
        public async Task ThenTheMatchAppliesTo(string from, string to, string projects)
        {
            await _web.OpenAsync(_setUrl);
            Assert.Equal(projects, Approved(from, to).QuerySelector(".match-applies-to")?.TextContent.Trim());
        }

        // From the Frameworks page to the pair page, and its link to several frameworks (every framework ticked)
        private async Task OpenSeveralAsync()
        {
            await _web.OpenAsync("/Frameworks");
            await _web.OpenAsync(_web.Page!.QuerySelector("#cross-mapping-link")!.GetAttribute("href")!);
            _setUrl = _web.Page!.QuerySelector("#several-frameworks")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException("The Cross-mapping page has no link to several frameworks.");
            await _web.OpenAsync(_setUrl);
        }

        // The page's own checkboxes give the version ids; the labels start with the framework's short name
        private string SetUrl(IReadOnlyList<string> frameworks)
        {
            var boxes = _web.Page!.QuerySelectorAll("form#choose-frameworks input[name='v']").ToList();
            var ids = frameworks.Select(name => boxes.FirstOrDefault(b => b.ParentElement!.TextContent.Trim().StartsWith(name + " v", StringComparison.Ordinal))
                    ?.GetAttribute("value") ?? throw new Xunit.Sdk.XunitException($"The Cross-mapping page has no framework '{name}'."))
                .ToList();
            return "/Frameworks/CrossMapping?" + string.Join("&", ids.Select(id => $"v={id}"));
        }

        private async Task<IElement> GroupAsync(string code)
        {
            await _web.OpenAsync(_setUrl);
            return _web.Page!.QuerySelector($"#match-groups li.match-group[data-code='{code}']")
                ?? throw new Xunit.Sdk.XunitException($"No pending matches of '{code}'.");
        }

        private IElement Approved(string from, string to)
            => _web.Page!.QuerySelector($"#approved-matches li.approved-match[data-from='{from}'][data-to='{to}']")
               ?? throw new Xunit.Sdk.XunitException($"No approved match of '{from}' and '{to}'.");

        private async Task SubmitAsync(IElement form)
        {
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .Select(i => new KeyValuePair<string, string>(i.GetAttribute("name")!, i.GetAttribute("value") ?? ""))
                .ToList();
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }
    }
}
