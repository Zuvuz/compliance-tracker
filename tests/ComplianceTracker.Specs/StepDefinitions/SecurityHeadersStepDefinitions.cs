using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using ComplianceTracker.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-087: the headers set by SecurityHeaders (Web/Security) on every response
    [Binding]
    public class SecurityHeadersStepDefinitions
    {
        private static readonly string[] Tabs = ["overview", "frameworks", "compliance", "risks", "product-requirements", "activity"];

        private readonly WebScenario _web;
        private readonly List<(string Path, IDocument Page)> _pages = new();

        public SecurityHeadersStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the address {string}")]
        public Task WhenIOpenTheAddress(string path)
            => _web.OpenAsync(path);

        [When("I open every tab of {string} and the frameworks page")]
        public async Task WhenIOpenEveryTab(string projectName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();

            foreach (var path in Tabs.Select(tab => $"/Projects/Details/{id}/{tab}").Concat(["/", "/Projects", "/Frameworks", $"/Projects/Edit/{id}"]))
            {
                await _web.OpenAsync(path);
                Assert.True(_web.Response!.IsSuccessStatusCode, $"{path} gave {_web.Response.StatusCode}");
                _pages.Add((path, _web.Page!));
            }
        }

        [Then("the response has a Content-Security-Policy with {string}")]
        public void ThenTheResponseHasACspWith(string directive)
            => Assert.Contains(directive, Policy());

        [Then("the Content-Security-Policy has no {string} and no {string}")]
        public void ThenTheCspHasNo(string first, string second)
        {
            Assert.DoesNotContain(first, Policy());
            Assert.DoesNotContain(second, Policy());
        }

        [Then("the response has {string} {string}")]
        public void ThenTheResponseHas(string header, string value)
            => Assert.Equal(value, _web.Response!.Headers.TryGetValues(header, out var values) ? string.Join(",", values) : null);

        // R2: the form-action directive names the origin of the configured Keycloak
        [Then("the Content-Security-Policy lets forms go to Keycloak")]
        public void ThenTheCspLetsFormsGoToKeycloak()
        {
            var authority = _web.App.Factory.Services.GetRequiredService<IOptions<KeycloakOptions>>().Value.Authority;
            var origin = new Uri(authority).GetLeftPart(UriPartial.Authority);
            var formAction = Policy().Split(';').Select(d => d.Trim()).Single(d => d.StartsWith("form-action"));
            Assert.Contains(origin, formAction);
        }

        // R4: what a policy without 'unsafe-inline' would block
        [Then("no page has an inline script, a style attribute or an event handler attribute")]
        public void ThenNoPageHasInlineCode()
        {
            var found = _pages.SelectMany(p =>
                    p.Page.QuerySelectorAll("script:not([src])").Select(_ => $"{p.Path}: inline <script>")
                    .Concat(p.Page.QuerySelectorAll("[style]").Select(e => $"{p.Path}: style on <{e.LocalName}>"))
                    .Concat(p.Page.All.SelectMany(e => e.Attributes.Where(a => a.Name.StartsWith("on")).Select(a => $"{p.Path}: {a.Name} on <{e.LocalName}>"))))
                .ToList();

            Assert.True(found.Count == 0, "Inline code the Content-Security-Policy blocks:\n" + string.Join("\n", found));
        }

        private string Policy()
            => _web.Response!.Headers.TryGetValues("Content-Security-Policy", out var values)
                ? string.Join(",", values)
                : throw new Xunit.Sdk.XunitException($"{_web.CurrentPath} has no Content-Security-Policy.");
    }
}
