using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-090: removing a member on the Users page (Views/Users/Index.cshtml: rows tr[data-member] with form.remove-member).
    // The removed person signs in in their own browser (an extra client signed in as them).
    [Binding]
    public class RemoveMemberStepDefinitions
    {
        private readonly WebScenario _web;
        private string? _otherPage;

        public RemoveMemberStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have removed {string}")]
        [When("I remove {string}")]
        public async Task WhenIRemove(string name)
        {
            await _web.OpenAsync("/Users");
            var form = _web.Page!.QuerySelector($"#organisation-users tr[data-member='{name}'] form.remove-member")
                ?? throw new Xunit.Sdk.XunitException($"The Users page has no way to remove {name}.");
            var fields = form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        // The person's own browser, which follows redirects like a browser does
        [When("{string} opens {string}")]
        public async Task WhenOpens(string name, string path)
        {
            using var client = _web.CreateClient(signedInAs: name);
            var response = await client.GetAsync(path);
            _otherPage = (await WebScenario.ParseAsync(response)).Body?.TextContent;
        }

        // In the person's own browser, through the create form (the removed membership must not block it)
        [When("{string} creates the organisation {string}")]
        public async Task WhenCreatesTheOrganisation(string name, string organisation)
        {
            using var client = _web.CreateClient(signedInAs: name);
            var form = await WebScenario.ParseAsync(await client.GetAsync("/Organizations/Create"));
            var token = form.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value")
                ?? throw new Xunit.Sdk.XunitException($"{name} gets no create form.");
            var response = await client.PostAsync("/Organizations/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Name"] = organisation,
                ["Cvr"] = "87654321",
                ["Sector"] = "Manufacturing",
                ["Size"] = "Small"
            }));
            Assert.Equal("/Projects", response.RequestMessage!.RequestUri!.AbsolutePath);
        }

        [Then("{string} sees {string}")]
        public void ThenSees(string name, string text)
            => Assert.Contains(text, _otherPage ?? "");

        [Then("{string} is not listed among the members")]
        public async Task ThenIsNotListed(string name)
        {
            await _web.OpenAsync("/Users");
            Assert.Null(_web.Page!.QuerySelector($"#organisation-users tr[data-member='{name}']"));
        }

        // Set up directly: the owner is chosen from the members, which the risk form already tests (US-042)
        [Given("the risk {string} is owned by {string}")]
        public async Task GivenTheRiskIsOwnedBy(string title, string name)
        {
            await using var db = _web.CreateDbContext();
            var ownerId = await MemberIdAsync(db, name);
            await db.Risks.Where(r => r.Title == title).ExecuteUpdateAsync(s => s.SetProperty(r => r.OwnerId, ownerId));
        }

        // R5: on the risk's own page and in the project's register
        [Then("the risk {string} shows the owner {string}")]
        public async Task ThenTheRiskShowsTheOwner(string title, string owner)
        {
            await using var db = _web.CreateDbContext();
            var risk = await db.Risks.Where(r => r.Title == title).Select(r => new { r.Id, r.ProjectId }).SingleAsync();

            await _web.OpenAsync($"/Risks/Details/{risk.Id}");
            Assert.Equal(owner, _web.Page!.QuerySelector("#risk-owner")?.TextContent.Trim());

            await _web.OpenAsync($"/Projects/Details/{risk.ProjectId}/risks");
            Assert.Contains(owner, _web.Page!.QuerySelectorAll(".risk-owner").Select(c => c.TextContent.Trim()));
        }

        [Then("{string} cannot be chosen as a risk's owner in {string}")]
        public async Task ThenCannotBeChosenAsOwner(string name, string projectName)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == projectName).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Risks/Add?projectId={projectId}");

            var choices = _web.Page!.QuerySelectorAll("select[name=OwnerId] option").Select(o => o.TextContent.Trim()).ToList();
            Assert.NotEmpty(choices);
            Assert.DoesNotContain(name, choices);
        }

        private static async Task<Guid> MemberIdAsync(Infrastructure.Persistence.ComplianceTrackerDbContext db, string name)
            => (await db.Organizations.ToListAsync()).SelectMany(o => o.Users).Single(u => u.Name == name).Id;
    }
}
