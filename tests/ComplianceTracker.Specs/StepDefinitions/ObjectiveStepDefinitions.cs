using ComplianceTracker.Application.Isms;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-209: the objectives page (/Isms/Objectives). UI contract: Views/Isms/Objectives.cshtml (#objectives rows with
    // data-objective and data-colour, .objective-value, .objective-due; form#add-objective, form.record-measurement,
    // #objectives-read-only). "The tracker takes its measurements" runs what the background job (ObjectiveWorker) runs.
    [Binding]
    public class ObjectiveStepDefinitions
    {
        private const string Path = "/Isms/Objectives";
        private readonly WebScenario _web;

        public ObjectiveStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I add the objective {string} measured by the tracker as {string} with the target at most {int}")]
        [When("I add the objective {string} measured by the tracker as {string} with the target at most {int}")]
        public Task WhenIAddTrackerObjective(string text, string measure, int target) => AddAsync(text, measure, target, "", "LowerIsBetter");

        [Given("the manual objective {string} with the target at most {int} {string}")]
        public Task GivenManualObjective(string text, int target, string unit) => AddAsync(text, "", target, unit, "LowerIsBetter");

        [Given("the tracker takes its measurements")]
        [When("the tracker takes its measurements")]
        public async Task WhenTheTrackerMeasures()
        {
            await using var scope = _web.App.Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ObjectiveService>().MeasureDueAsync();
        }

        [When("I enter the measurement {string} for {string}")]
        public async Task WhenIEnter(string value, string text)
        {
            await _web.OpenAsync(Path);
            await using var db = _web.CreateDbContext();
            var id = await db.SecurityObjectives.Where(o => o.Text == text).Select(o => o.Id).SingleAsync();
            await _web.PostFormAsync("/Isms/RecordMeasurement", new Dictionary<string, string> { ["objectiveId"] = id.ToString(), ["value"] = value });
        }

        [When("I open the objectives")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        [Then("the objective {string} shows {string} in {word}")]
        public async Task ThenShows(string text, string value, string colour)
        {
            await _web.OpenAsync(Path);
            var row = Row(text);
            Assert.Equal(value, row.QuerySelector(".objective-value span")?.TextContent.Trim());
            Assert.Equal(colour, row.GetAttribute("data-colour"));
        }

        [Then("the objective {string} has {int} measurement(s)")]
        public async Task ThenHasMeasurements(string text, int count)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(count, await db.SecurityObjectives.Where(o => o.Text == text).SelectMany(o => o.Measurements).CountAsync());
        }

        [Then("the objective {string} is due for a measurement")]
        public void ThenDue(string text) => Assert.NotNull(Row(text).QuerySelector(".objective-due"));

        [Then("the objectives page says {string}")]
        public void ThenSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("the objectives page is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#objectives-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#add-objective, form.record-measurement, form.change-objective"));
        }

        private async Task AddAsync(string text, string measure, int target, string unit, string direction)
        {
            await _web.OpenAsync(Path);
            Guid ownerId;
            await using (var db = _web.CreateDbContext())
            {
                ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == "Anna").Select(u => u.Id).FirstAsync();
            }

            await _web.PostFormAsync("/Isms/AddObjective", new Dictionary<string, string>
            {
                ["text"] = text,
                ["ownerId"] = ownerId.ToString(),
                ["measure"] = measure,
                ["target"] = target.ToString(),
                ["unit"] = unit,
                ["direction"] = direction,
                ["frequency"] = "Monthly"
            });
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));
        }

        private AngleSharp.Dom.IElement Row(string text)
            => _web.Page!.QuerySelector($"#objectives tr[data-objective='{text}']")
               ?? throw new Xunit.Sdk.XunitException($"No objective '{text}'.");
    }
}
