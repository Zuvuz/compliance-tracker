using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-092: forms carry the version they showed (a hidden "Version" or "rowVersion" field).
    // UI contract: Views/Risks/_RiskForm.cshtml, Views/Projects/Edit.cshtml, Views/Frameworks/Version.cshtml.
    // "Someone else" changes the row with an UPDATE through EF Core, which gives it a new xmin like any other save.
    [Binding]
    public class ConcurrentChangesStepDefinitions
    {
        private readonly WebScenario _web;

        // The page the user opened, kept while "someone else" changes the data
        private IDocument? _opened;

        public ConcurrentChangesStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("I have opened the risk {string} for editing")]
        public async Task GivenIHaveOpenedTheRiskForEditing(string title)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
            await OpenAsync($"/Risks/Edit/{id}");
        }

        [Given("I have opened the project {string} for editing")]
        public async Task GivenIHaveOpenedTheProjectForEditing(string name)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == name).Select(p => p.Id).SingleAsync();
            await OpenAsync($"/Projects/Edit/{id}");
        }

        // The draft's page is the one the upload ended on (US-007)
        [Given("I have the draft's page open")]
        public void GivenIHaveTheDraftsPageOpen()
            => _opened = _web.Page;

        [Given("someone else changes the impact of {string} to {int}")]
        public async Task GivenSomeoneElseChangesTheImpact(string title, int impact)
        {
            await using var db = _web.CreateDbContext();
            await db.Risks.Where(r => r.Title == title).ExecuteUpdateAsync(s => s.SetProperty(r => r.Impact, impact));
        }

        [Given("someone else renames the project {string} to {string}")]
        public async Task GivenSomeoneElseRenamesTheProject(string name, string newName)
        {
            await using var db = _web.CreateDbContext();
            await db.Projects.Where(p => p.Name == name).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, newName));
        }

        [Given("someone else changes the title of requirement {string} to {string}")]
        public async Task GivenSomeoneElseChangesTheTitleOfRequirement(string code, string title)
        {
            await using var db = _web.CreateDbContext();
            await db.Requirements.Where(r => r.Code == code).ExecuteUpdateAsync(s => s.SetProperty(r => r.Title, title));
        }

        // ---------------------------------------------------------- When

        [When("I save the description {string}")]
        public Task WhenISaveTheDescription(string description)
            => SubmitAsync(_opened!.QuerySelector("form[method=post]:has(textarea[name=Description])")!, "Description", description);

        [When("I save the scope {string}")]
        public Task WhenISaveTheScope(string scope)
            => SubmitAsync(_opened!.QuerySelector("form[method=post]:has([name=Scope])")!, "Scope", scope);

        // The row's own form, with the inputs that point at it (form="...")
        [When("I save the title {string} for {string} from the page I opened")]
        public Task WhenISaveTheTitleFromThePageIOpened(string title, string code)
        {
            var row = _opened!.QuerySelector($"tr[data-requirement-code='{code}']")
                ?? throw new Xunit.Sdk.XunitException($"The draft's page has no row {code}.");
            var form = row.QuerySelector("form[action*='ChangeRequirement']")!;
            return SubmitAsync(form, "title", title);
        }

        // ---------------------------------------------------------- Then

        [Then("the impact of {string} is still {int}")]
        public async Task ThenTheImpactIsStill(string title, int impact)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(impact, await db.Risks.Where(r => r.Title == title).Select(r => r.Impact).SingleAsync());
        }

        [Then("the description of {string} is {string}")]
        public async Task ThenTheDescriptionIs(string title, string description)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(description, await db.Risks.Where(r => r.Title == title).Select(r => r.Description).SingleAsync());
        }

        // R3: what the user typed is not lost
        [Then("the field {string} still contains {string}")]
        public void ThenTheFieldStillContains(string name, string value)
        {
            var field = _web.Page!.QuerySelector($"[name='{name}']");
            var shown = field is IHtmlTextAreaElement area ? area.Value : field?.GetAttribute("value");
            Assert.Equal(value, shown?.Trim());
        }

        [Then("the project is still called {string}")]
        public async Task ThenTheProjectIsStillCalled(string name)
        {
            await using var db = _web.CreateDbContext();
            Assert.True(await db.Projects.AnyAsync(p => p.Name == name), $"No project is called {name}.");
        }

        [Then("the requirement {string} is still called {string}")]
        public async Task ThenTheRequirementIsStillCalled(string code, string title)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(title, await db.Requirements.Where(r => r.Code == code).Select(r => r.Title).SingleAsync());
        }

        // ---------------------------------------------------------- Helpers

        private async Task OpenAsync(string path)
        {
            await _web.OpenAsync(path);
            _opened = _web.Page;
        }

        // Sends the form as the browser would, with every field as it was when the page was opened and one field changed
        private async Task SubmitAsync(IElement form, string field, string value)
        {
            var fields = Fields(form);
            fields[field] = value;
            fields.Remove("__RequestVerificationToken"); // PostFormAsync takes it from the page shown last, which is this one
            _web.Page = _opened;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private static Dictionary<string, string> Fields(IElement form)
        {
            var id = form.GetAttribute("id");
            var elements = form.QuerySelectorAll("input[name], textarea[name], select[name]")
                .Concat(id is null ? [] : form.Owner!.QuerySelectorAll($"[form='{id}'][name]"));

            var fields = new Dictionary<string, string>();
            foreach (var element in elements)
            {
                var value = element switch
                {
                    IHtmlTextAreaElement area => area.Value,
                    IHtmlSelectElement select => select.Value,
                    IHtmlInputElement { Type: "checkbox" or "radio", IsChecked: false } => null,
                    IHtmlInputElement input => input.Value,
                    _ => null
                };
                if (value is not null)
                {
                    fields[element.GetAttribute("name")!] = value;
                }
            }

            return fields;
        }
    }
}
