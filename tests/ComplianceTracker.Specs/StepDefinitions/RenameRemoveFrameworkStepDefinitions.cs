using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // #52: Views/Frameworks/Index.cshtml (#frameworks tr[data-framework-id] with .framework-name, .framework-tags,
    // form.rename-framework (name) and form.remove-framework).
    [Binding]
    public class RenameRemoveFrameworkStepDefinitions
    {
        private readonly WebScenario _web;

        public RenameRemoveFrameworkStepDefinitions(WebScenario web) => _web = web;

        private async Task<IElement> RowAsync(string name)
        {
            await _web.OpenAsync("/Frameworks");
            return Row(name) ?? throw new Xunit.Sdk.XunitException($"The frameworks page does not list '{name}'.");
        }

        private IElement? Row(string name)
            => _web.Page!.QuerySelectorAll("#frameworks tr[data-framework-id]")
                .FirstOrDefault(r => r.QuerySelector(".framework-name")?.TextContent.Trim() == name);

        private Task PostAsync(IElement form, Dictionary<string, string> fields)
        {
            foreach (var input in form.QuerySelectorAll("input[type=hidden]").Where(i => i.GetAttribute("name") != "__RequestVerificationToken"))
            {
                fields.TryAdd(input.GetAttribute("name")!, input.GetAttribute("value") ?? "");
            }

            return _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [When("I rename the framework {string} to {string}")]
        public async Task WhenIRename(string name, string newName)
        {
            var form = (await RowAsync(name)).QuerySelector("form.rename-framework") ?? throw new Xunit.Sdk.XunitException("No \"Rename\".");
            await PostAsync(form, new Dictionary<string, string> { ["name"] = newName });
        }

        [When("I remove the framework {string}")]
        public async Task WhenIRemove(string name)
        {
            var form = (await RowAsync(name)).QuerySelector("form.remove-framework") ?? throw new Xunit.Sdk.XunitException("No \"Remove\".");
            await PostAsync(form, []);
        }

        [Then("the frameworks page lists {string} with the test tags {string}")]
        public async Task ThenListsWithTags(string name, string tags)
        {
            var row = await RowAsync(name);
            Assert.Equal($"Test tags: {tags}", row.QuerySelector(".framework-tags")?.TextContent.Trim());
        }

        [Then("the frameworks page does not list {string}")]
        public async Task ThenDoesNotList(string name)
        {
            await _web.OpenAsync("/Frameworks");
            Assert.Null(Row(name));
        }
    }
}
