using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-204: the requirement page's .proof-state (or .requirement-status in the organisation area), the lists
    // .evidence-kind-list[data-kind] with the evidence after them, .documentation-enough with .documentation-enough-reason
    // and form.decide-documentation-enough; the Evidence tab's .evidence-kind[data-kind] and form.change-evidence-kind;
    // the Documents page's .document-suggestions-title and form.approve-document-suggestion.
    [Binding]
    public class DocumentedVersusProvenStepDefinitions
    {
        private readonly WebScenario _web;

        public DocumentedVersusProvenStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I mark {string} of {string} as {string} with the reason {string}")]
        public async Task WhenIMarkDocumentation(string code, string project, string decision, string reason)
        {
            await OpenRequirementAsync(project, code);
            var form = _web.Page!.QuerySelector("form.decide-documentation-enough")
                ?? throw new Xunit.Sdk.XunitException($"{code} cannot be marked.");
            var enough = decision switch
            {
                "documentation is enough" => "true",
                "proof is needed" => "false",
                _ => throw new ArgumentException(decision)
            };
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            Assert.Equal(enough, fields["enough"]);
            fields["reason"] = reason;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            await OpenRequirementAsync(project, code);
        }

        [When("I mark the evidence {string} of {string} as documentation")]
        public async Task WhenIMarkTheEvidenceAsDocumentation(string title, string project)
        {
            await OpenEvidenceTabAsync(project);
            var form = Card(title).QuerySelector("form.change-evidence-kind")
                ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be changed.");
            Assert.Equal("Documentation", form.QuerySelector("input[name=kind]")!.GetAttribute("value"));
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the requirement shows {string}")]
        public void ThenTheRequirementShows(string state)
        {
            var shown = _web.Page!.QuerySelectorAll(".requirement-header .proof-state, .requirement-header .requirement-status")
                .Select(e => e.TextContent.Trim()).ToList();
            Assert.Contains(state, shown);
        }

        [Then("{string} lists {string}")]
        public void ThenTheListHas(string list, string title)
        {
            var heading = _web.Page!.QuerySelectorAll(".evidence-kind-list").FirstOrDefault(h => h.TextContent.Trim().StartsWith(list, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"The requirement has no \"{list}\".");
            var titles = new List<string>();
            for (var next = heading.NextElementSibling; next is not null && !next.ClassList.Contains("evidence-kind-list"); next = next.NextElementSibling)
            {
                if (next.ClassList.Contains("evidence"))
                {
                    titles.Add(next.GetAttribute("data-title") ?? "");
                }
            }

            Assert.Contains(title, titles);
        }

        [Then("the step for the requirement's evidence asks to {string}")]
        public void ThenTheStepAsks(string action)
        {
            var step = _web.Page!.QuerySelectorAll("#requirement-path .path-step").FirstOrDefault(s => s.QuerySelector(".path-step-text")?.TextContent.Trim() == "Evidence on the requirement")
                ?? throw new Xunit.Sdk.XunitException("The requirement has no evidence step.");
            Assert.Equal("false", step.GetAttribute("data-done"));
            Assert.Equal(action, step.QuerySelector(".path-step-action")?.TextContent.Trim());
        }

        [Then("the requirement says documentation is enough because {string}")]
        public void ThenDocumentationIsEnough(string reason)
        {
            var mark = _web.Page!.QuerySelector(".documentation-enough") ?? throw new Xunit.Sdk.XunitException("The requirement has no documentation mark.");
            Assert.Equal("true", mark.GetAttribute("data-enough"));
            Assert.Equal(": " + reason, mark.QuerySelector(".documentation-enough-reason")?.TextContent.Trim());
        }

        [Then("the evidence {string} of {string} is {string}")]
        public async Task ThenTheEvidenceIs(string title, string project, string kind)
        {
            await OpenEvidenceTabAsync(project);
            Assert.Equal(kind, Card(title).QuerySelector(".evidence-kind")?.TextContent.Trim());
        }

        [Then("the Documents page heads the AI's suggestions {string} and offers {string}")]
        public async Task ThenTheDocumentsPageHeads(string heading, string button)
        {
            await _web.OpenAsync("/Documents");
            var title = _web.Page!.QuerySelector(".document-suggestions-title") ?? throw new Xunit.Sdk.XunitException("No AI suggestions on the Documents page.");
            Assert.StartsWith(heading, title.TextContent.Trim());
            Assert.NotNull(_web.Page!.QuerySelector(".document-suggestions-note"));
            Assert.Equal(button, _web.Page!.QuerySelector("form.approve-document-suggestion button")?.TextContent.Trim());
        }

        private AngleSharp.Dom.IElement Card(string title)
            => _web.Page!.QuerySelectorAll(".evidence-item").FirstOrDefault(c => c.GetAttribute("data-title") == title)
               ?? throw new Xunit.Sdk.XunitException($"The Evidence tab has no '{title}'.");

        private async Task OpenRequirementAsync(string project, string code)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance/cra/{Uri.EscapeDataString(code)}");

        private async Task OpenEvidenceTabAsync(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}?tab=evidence");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
