using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-213: competence and awareness (/Isms/Competence). UI contract: Views/Isms/Competence.cshtml (#policies-to-read items
    // with data-policy and form.confirm-policy, #competence-matrix rows with data-person and cells with data-competence and
    // data-state, #policy-readers rows with data-policy and .policy-read, form#add-competence, form#record-training,
    // form#add-staff-person, #competence-read-only). A member confirms a policy in their own browser.
    [Binding]
    public class CompetenceStepDefinitions
    {
        private const string Path = "/Isms/Competence";
        private readonly WebScenario _web;

        public CompetenceStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("{string} has confirmed the policy {string}")]
        [When("{string} has confirmed the policy {string}")]
        public async Task GivenConfirmed(string name, string policy)
        {
            using var client = _web.CreateClient(signedInAs: name);
            var page = await WebScenario.ParseAsync(await client.GetAsync(Path));
            var form = page.QuerySelector($"#policies-to-read [data-policy='{policy}'] form.confirm-policy")
                       ?? throw new Xunit.Sdk.XunitException($"{name} has no '{policy}' to read.");
            var response = await client.PostAsync(form.GetAttribute("action"), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = form.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!,
                ["documentId"] = form.QuerySelector("input[name='documentId']")!.GetAttribute("value")!
            }));
            Assert.True(response.IsSuccessStatusCode);
        }

        [Then("{string} must read the policy {string}")]
        public async Task ThenMustRead(string name, string policy)
        {
            await _web.OpenAsync(Path);
            Assert.Contains(name, Readers(policy, ".policy-not-read"), StringComparison.Ordinal);
        }

        [Then("{string} has read the policy {string}")]
        public async Task ThenHasRead(string name, string policy)
        {
            await _web.OpenAsync(Path);
            Assert.Contains(name, Readers(policy, ".policy-read"), StringComparison.Ordinal);
            Assert.DoesNotContain(name, Readers(policy, ".policy-not-read"), StringComparison.Ordinal);
        }

        [Given("the competence {string} must be renewed every year")]
        public Task GivenYearly(string name)
            => PostAsync("/Isms/AddCompetence", [new("name", name), new("renewalMonths", "12"), new("roles", "Developer"), new("roles", "ComplianceManager")]);

        [Given("the competence {string} for people without a user")]
        public Task GivenForPeople(string name) => PostAsync("/Isms/AddCompetence", [new("name", name), new("forPeopleWithoutUser", "true")]);

        [Given("I add {string} who has no user")]
        public Task GivenPerson(string name) => PostAsync("/Isms/AddStaffPerson", [new("name", name)]);

        [Given("{string} was trained in {string} on {string}")]
        public async Task GivenTrained(string name, string competence, string on)
        {
            await using var db = _web.CreateDbContext();
            var userId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == name).Select(u => u.Id).FirstAsync();
            var competenceId = await db.Competences.Where(c => c.Name == competence).Select(c => c.Id).SingleAsync();
            await PostAsync("/Isms/RecordTraining", [new("competenceId", competenceId.ToString()), new("person", $"user:{userId}"), new("trainedOn", on)]);
        }

        [When("I open the competence overview")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("{string} is {string} in {string}")]
        public void ThenState(string person, string state, string competence)
        {
            var cell = _web.Page!.QuerySelector($"#competence-matrix tr[data-person='{person}'] td[data-competence='{competence}']")
                       ?? throw new Xunit.Sdk.XunitException($"No '{person}' and '{competence}' in the matrix.");
            Assert.Equal(state, cell.GetAttribute("data-state"));
        }

        [Then("the competence overview is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#competence-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#add-competence, form#record-training, form#add-staff-person"));
        }

        private string Readers(string policy, string column)
            => _web.Page!.QuerySelector($"#policy-readers tr[data-policy='{policy}'] {column}")?.TextContent
               ?? throw new Xunit.Sdk.XunitException($"No policy '{policy}'.");

        private async Task PostAsync(string path, List<KeyValuePair<string, string>> fields)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync(path, fields);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }
    }
}
