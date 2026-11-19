using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // #52: the Overview's #phase-card with the badge #phase-gate-state, the checklist #checklist, and the form #change-phase
    // (button #move-phase with [data-phase-target], details.other-phase with select[data-phase-select], parts [data-show-for])
    [Binding]
    public class PhaseCardStepDefinitions
    {
        private readonly WebScenario _web;

        public PhaseCardStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the phase card's main button says {string} with items left")]
        public void ThenTheMainButtonSays(string text)
        {
            var button = Form().QuerySelector("#move-phase") ?? throw new Xunit.Sdk.XunitException("The phase card has no main button.");
            var label = Normalize(button.TextContent);
            Assert.StartsWith(text + " (", label);
            Assert.EndsWith(" left)", label);
            Assert.Contains("left before", _web.Page!.QuerySelector("#phase-card #phase-gate-state")?.TextContent ?? "");
        }

        [Then("the phase card offers the other phases {string}")]
        public void ThenTheCardOffers(string phases)
        {
            var menu = Form().QuerySelector("details.other-phase") ?? throw new Xunit.Sdk.XunitException("The phase card has no \"Other phase…\".");
            Assert.Equal("Other phase…", menu.QuerySelector("summary")!.TextContent.Trim());
            Assert.Equal(phases, string.Join(", ", menu.QuerySelectorAll("select[name=phase] option").Select(o => o.TextContent.Trim())));
            Assert.Equal(Normalize(Form().QuerySelector("[data-phase-target]")!.TextContent),
                menu.QuerySelector("select[name=phase] option[selected]")?.TextContent.Trim());
        }

        // Hidden in the page; the page's script shows it when the phase is chosen under "Other phase…"
        [Then("the phase card hides the fields for {string} until it is chosen")]
        public void ThenTheFieldsAreHidden(string phase)
        {
            Assert.True(Part(phase).HasAttribute("hidden"), $"The fields for {phase} are shown before {phase} is chosen.");
            Assert.NotNull(Form().QuerySelector($"select[data-phase-select] option[value='{phase}']"));
        }

        [Then("the phase card shows the fields for {string}")]
        public void ThenTheFieldsShow(string phase)
            => Assert.False(Part(phase).HasAttribute("hidden"), $"The fields for {phase} are hidden while it is the next phase.");

        [When("I tick the checklist item {string} of {string} with the project's evidence {string}")]
        public async Task WhenITickWithEvidence(string item, string project, string evidence)
        {
            await using (var db = _web.CreateDbContext())
            {
                var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
                await _web.OpenAsync($"/Projects/Details/{id}");
            }

            var form = Item(item).QuerySelector("form.checklist-change[data-change='tick']")
                ?? throw new Xunit.Sdk.XunitException($"'{item}' cannot be ticked.");
            var option = form.QuerySelectorAll("details.checklist-evidence select[name=evidencePick] option").FirstOrDefault(o => o.TextContent.Trim() == evidence)
                ?? throw new Xunit.Sdk.XunitException($"'+ Evidence' on '{item}' does not offer '{evidence}'.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            fields["evidencePick"] = option.GetAttribute("value")!;
            fields["evidenceUrl"] = "";
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the checklist item {string} links to the evidence {string}")]
        public async Task ThenTheItemLinksToTheEvidence(string item, string evidence)
        {
            await using var db = _web.CreateDbContext();
            var evidenceId = await db.Evidence.Where(e => e.Title == evidence).Select(e => e.Id).SingleAsync();
            var link = Item(item).QuerySelector(".checklist-note a") ?? throw new Xunit.Sdk.XunitException($"'{item}' has no evidence link.");
            Assert.Contains(evidenceId.ToString(), link.GetAttribute("href"));
        }

        private AngleSharp.Dom.IElement Form()
            => _web.Page!.QuerySelector("#phase-card form#change-phase") ?? throw new Xunit.Sdk.XunitException("The phase card has no phase form.");

        private AngleSharp.Dom.IElement Part(string phase)
            => Form().QuerySelector($"[data-show-for='{phase}']") ?? throw new Xunit.Sdk.XunitException($"The phase form has no fields for {phase}.");

        private AngleSharp.Dom.IElement Item(string text)
            => _web.Page!.QuerySelectorAll("#phase-card #checklist .checklist-item").FirstOrDefault(i => i.QuerySelector(".checklist-text")?.TextContent.Trim() == text)
               ?? throw new Xunit.Sdk.XunitException($"The phase card's checklist has no '{text}'.");

        private static string Normalize(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
