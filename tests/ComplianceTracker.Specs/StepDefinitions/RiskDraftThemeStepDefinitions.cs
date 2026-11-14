using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-157: the risk agent's rounds per theme. UI contract: Views/Projects/_RisksTab.cshtml (#risk-drafts with
    // h3.risk-draft-theme[data-theme] before its .risk-draft[data-title], #risks-drafted for the message).
    [Binding]
    public class RiskDraftThemeStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;

        public RiskDraftThemeStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        // The pretend agent answers the theme's round with this one risk (the other themes keep their default answer
        // unless they get their own)
        [Given("the AI drafts {string} for {string}")]
        public void GivenTheAiDrafts(string title, string theme)
        {
            // A theme without its own answer would give the default two risks; only the themes named answer
            foreach (var other in new[] { "Access and sign-in", "Data and privacy", "Updates and supply chain", "Availability", "Requirements not met", "Events and findings" })
            {
                _ai.RiskDraftAnswers.TryAdd(other, "Nothing to add");
            }

            _ai.RiskDraftAnswers[theme] = $"RISK: {title}\nTHREAT: Someone abuses it\nASSET: The product\nLIKELIHOOD: 3\nIMPACT: 4\nBASED ON: {theme}\nWHY: Because";
        }

        [Given("the AI cannot be reached for {string}")]
        public void GivenTheAiCannotBeReachedFor(string theme)
            => _ai.RiskThemesDown.Add(theme);

        [Then("the Risks tab of {string} shows {string} under {string}")]
        public async Task ThenShowsUnder(string project, string title, string theme)
        {
            await OpenAsync(project);
            Assert.Equal(theme, ThemeOf(title));
        }

        [Then("the Risks tab of {string} has the draft {string} once")]
        public async Task ThenHasTheDraftOnce(string project, string title)
        {
            await OpenAsync(project);
            Assert.Single(_web.Page!.QuerySelectorAll("#risk-drafts .risk-draft"), d => d.GetAttribute("data-title") == title);
        }

        [Then("the AI was asked about {string}, {string}, {string}, {string} and {string}")]
        public void ThenTheAiWasAskedAbout(string a, string b, string c, string d, string e)
        {
            var asked = _ai.Prompts
                .Where(p => p.Contains(ComplianceTracker.Application.Risks.RiskDraftPrompt.Marker, StringComparison.Ordinal))
                .Select(p => p[(p.IndexOf("Theme: ", StringComparison.Ordinal) + "Theme: ".Length)..p.IndexOf('.', p.IndexOf("Theme: ", StringComparison.Ordinal))])
                .ToList();
            Assert.Equal([a, b, c, d, e], asked);
        }

        // The theme heading the draft stands under
        private string? ThemeOf(string title)
        {
            string? theme = null;
            foreach (var element in _web.Page!.QuerySelectorAll("#risk-drafts .risk-draft-theme, #risk-drafts .risk-draft"))
            {
                if (element.ClassList.Contains("risk-draft-theme"))
                {
                    theme = element.GetAttribute("data-theme");
                }
                else if (element.GetAttribute("data-title") == title)
                {
                    return theme;
                }
            }

            throw new Xunit.Sdk.XunitException($"No draft '{title}' on the Risks tab.");
        }

        private async Task OpenAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/risks");
        }
    }
}
