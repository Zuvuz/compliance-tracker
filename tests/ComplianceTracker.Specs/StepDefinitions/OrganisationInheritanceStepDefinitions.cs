using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-115: a product's "From the organisation" page (/Projects/Inherited/{id}): tr.inherited-row[data-code][data-framework]
    // with .inherited-status, .inherited-label, .opt-out-reason and the forms .inherited-opt-out, .inherited-opt-in and
    // .inherited-link-proof; the organisation's requirement page with .requirement-scope, form.change-requirement-scope and
    // .carried-out.
    [Binding]
    public class OrganisationInheritanceStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly OrganisationNis2StepDefinitions _organisation;

        public OrganisationInheritanceStepDefinitions(WebScenario web, OrganisationNis2StepDefinitions organisation)
        {
            _web = web;
            _organisation = organisation;
        }

        [When("I open what {string} inherits from the organisation")]
        public async Task WhenIOpenInherited(string project)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/compliance");
            var link = _web.Page!.QuerySelector("#from-organisation-link") ?? throw new Xunit.Sdk.XunitException("The Compliance tab has no \"From the organisation\".");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I mark the inherited {string} as not applicable to {string} because {string}")]
        public async Task WhenIOptOut(string code, string project, string reason)
        {
            await WhenIOpenInherited(project);
            await PostAsync(Row(code, "NIS2").QuerySelector("form.inherited-opt-out") ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be marked."),
                new() { ["reason"] = reason });
        }

        [When("I make the inherited {string} applicable to {string} again")]
        public async Task WhenIOptIn(string code, string project)
        {
            await WhenIOpenInherited(project);
            await PostAsync(Row(code, "NIS2").QuerySelector("form.inherited-opt-in") ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be made applicable."));
        }

        [When("I add {string} as proof of the inherited {string} for {string}")]
        public async Task WhenIAddProof(string evidence, string code, string project)
        {
            await WhenIOpenInherited(project);
            var form = Row(code, "NIS2").QuerySelector("form.inherited-link-proof") ?? throw new Xunit.Sdk.XunitException($"'{code}' takes no proof from {project}.");
            var option = form.QuerySelectorAll("select[name=evidenceId] option").FirstOrDefault(o => o.TextContent.Trim() == evidence)
                ?? throw new Xunit.Sdk.XunitException($"'{evidence}' cannot be picked.");
            await PostAsync(form, new() { ["evidenceId"] = option.GetAttribute("value")! });
        }

        [When("I carry out {string} of {string} in the projects because {string}")]
        public async Task WhenICarryOut(string code, string framework, string reason)
        {
            var page = await OrganisationRequirementAsync(code, framework);
            Assert.Equal("OrganisationWide", page.QuerySelector(".requirement-scope")?.GetAttribute("data-scope"));
            var form = page.QuerySelector("form.change-requirement-scope") ?? throw new Xunit.Sdk.XunitException($"Where '{code}' is met cannot be changed.");
            await PostAsync(form, new() { ["reason"] = reason });
        }

        [Then("the inherited {string} of {string} is {string} and {string}")]
        public void ThenTheInheritedIs(string code, string framework, string status, string label)
        {
            var row = Row(code, framework);
            Assert.Equal(status, row.QuerySelector(".inherited-status")?.TextContent.Trim());
            Assert.Equal(label, row.QuerySelector(".inherited-label")?.TextContent.Trim());
        }

        [Then("the organisation's {string} of {string} says {string}")]
        public async Task ThenTheOrganisationSays(string code, string framework, string text)
            => Assert.Equal(text, (await OrganisationRequirementAsync(code, framework)).QuerySelector(".carried-out")?.TextContent.Trim());

        private async Task<AngleSharp.Dom.IElement> OrganisationRequirementAsync(string code, string framework)
        {
            await _organisation.WhenIOpenTheOrganisationsComplianceTab(framework, "en");
            return await RequirementRowLookup.FindAsync(_web, code) ?? throw new Xunit.Sdk.XunitException($"The organisation has no '{code}'.");
        }

        private AngleSharp.Dom.IElement Row(string code, string framework)
            => _web.Page!.QuerySelectorAll("tr.inherited-row").FirstOrDefault(r => r.GetAttribute("data-code") == code && r.GetAttribute("data-framework") == framework)
               ?? throw new Xunit.Sdk.XunitException($"'{framework} {code}' is not inherited ({_web.CurrentPath}).");

        private async Task PostAsync(AngleSharp.Dom.IElement form, Dictionary<string, string>? more = null)
        {
            var fields = form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            foreach (var (key, value) in more ?? [])
            {
                fields[key] = value;
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
