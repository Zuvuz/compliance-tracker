using System.Text.Json;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-095: the privacy page (#personal-data li[data-item], #my-data-link), the download of my data (/Users/MyData), and
    // the erasure on the Users page (#removed-members li.removed-member with form.erase-personal-data)
    [Binding]
    public class PersonalDataStepDefinitions
    {
        private readonly WebScenario _web;
        private JsonDocument? _myData;

        public PersonalDataStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I open the privacy page")]
        public Task WhenIOpenThePrivacyPage() => _web.OpenAsync("/Home/Privacy");

        [When("I download my data")]
        public async Task WhenIDownloadMyData()
        {
            await _web.OpenAsync("/Home/Privacy");
            var href = _web.Page!.QuerySelector("#my-data-link")!.GetAttribute("href")!;
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            _myData = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        }

        [When("I erase the personal data of {string}")]
        public async Task WhenIErase(string name)
        {
            await _web.OpenAsync("/Users");
            var form = _web.Page!.QuerySelectorAll("#removed-members li.removed-member").First(m => m.GetAttribute("data-member") == name)
                .QuerySelector("form.erase-personal-data") ?? throw new Xunit.Sdk.XunitException($"{name}'s personal data cannot be erased.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["memberId"] = form.QuerySelector("input[name=memberId]")!.GetAttribute("value")! });
        }

        // The page offers no button for an active member; the request is sent anyway, so the server must refuse it
        [When("I send an erasure of {string} anyway")]
        public async Task WhenISendAnErasureAnyway(string name)
        {
            await _web.OpenAsync("/Users");
            await _web.PostFormAsync("/Users/ErasePersonalData", new Dictionary<string, string> { ["memberId"] = (await MemberIdAsync(name)).ToString() });
        }

        [When("{string} sends an erasure of {string}")]
        public async Task WhenSendsAnErasure(string userName, string name)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync("/Home/Privacy"));
            var token = page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value") ?? "";
            await client.PostAsync("/Users/ErasePersonalData", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["memberId"] = (await MemberIdAsync(name)).ToString(),
                ["__RequestVerificationToken"] = token
            }));
        }

        [Then("the privacy page lists {string}, {string}, {string}, {string} and {string}")]
        public void ThenThePrivacyPageLists(string a, string b, string c, string d, string e)
            => Assert.Equal([a, b, c, d, e], _web.Page!.QuerySelectorAll("#personal-data li").Select(li => li.GetAttribute("data-item")!));

        [Then("the privacy page links to {string}")]
        public void ThenThePrivacyPageLinksTo(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector("#my-data-link")!.TextContent, StringComparison.OrdinalIgnoreCase);

        [Then("my data has the name {string} and an entry {string}")]
        public void ThenMyDataHas(string name, string action)
        {
            var root = _myData!.RootElement;
            Assert.Equal(name, root.GetProperty("profile").GetProperty("Name").GetString());
            Assert.Contains(root.GetProperty("activity").EnumerateArray(), e => e.GetProperty("action").GetString() == action);
        }

        [Then("the activity log has {string} without the name {string}")]
        public async Task ThenTheLogHasWithoutTheName(string action, string name)
        {
            await using var db = _web.CreateDbContext();
            var expected = Enum.Parse<Domain.ActivityLog.ActivityAction>(action);
            var details = await db.ActivityLog.Where(e => e.Action == expected).Select(e => e.Details).ToListAsync();
            Assert.NotEmpty(details);
            Assert.All(details, d => Assert.DoesNotContain(name, d ?? "", StringComparison.Ordinal));
        }

        [Then("the removed members list a {string} with the personal data erased")]
        public async Task ThenTheRemovedMembersList(string pseudonym)
        {
            await _web.OpenAsync("/Users");
            Assert.Contains(_web.Page!.QuerySelectorAll("#removed-members li.removed-member"),
                m => m.GetAttribute("data-member")!.StartsWith(pseudonym, StringComparison.Ordinal) && m.TextContent.Contains("Personal data erased"));
        }

        [Then("no member is called {string} any more")]
        public async Task ThenNoMemberIsCalled(string name)
        {
            await using var db = _web.CreateDbContext();
            var users = (await db.Organizations.SingleAsync()).Users;
            Assert.DoesNotContain(users, u => u.Name == name || u.Email.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        [Then("{string} is still a member")]
        public async Task ThenIsStillAMember(string name)
        {
            await using var db = _web.CreateDbContext();
            Assert.Contains((await db.Organizations.SingleAsync()).Members, u => u.Name == name);
        }

        [Then("no personal data was erased")]
        public async Task ThenNoPersonalDataWasErased()
        {
            await using var db = _web.CreateDbContext();
            Assert.DoesNotContain((await db.Organizations.SingleAsync()).Users, u => u.IsErased);
        }

        private async Task<Guid> MemberIdAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return (await db.Organizations.SingleAsync()).Users.Single(u => u.Name == name).Id;
        }
    }
}
