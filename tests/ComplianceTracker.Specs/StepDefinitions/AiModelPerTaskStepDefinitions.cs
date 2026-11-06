using AngleSharp.Dom;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Infrastructure.Ai;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-117: a model per AI task. The web scenarios use the pretend AI under the task's name (FakeAiModels); the
    // settings scenario reads Ai:Tasks with the real ConfiguredAiModels, without calling any model.
    // UI contract: Views/System/Index.cshtml (#ai-models rows data-task with .ai-model and .ai-model-source).
    [Binding]
    public class AiModelPerTaskStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly FakeAi _ai;
        private IAiModels? _configured;

        public AiModelPerTaskStepDefinitions(WebScenario web, FakeAi ai)
        {
            _web = web;
            _ai = ai;
        }

        [Given("the AI task {string} has its own model {string}")]
        public void GivenTheTaskHasItsOwnModel(string task, string model)
            => _ai.TaskModels[Enum.Parse<AiTask>(task)] = model;

        [Given("the AI task {string} has its own model {string} that cannot be reached")]
        public void GivenTheTaskModelCannotBeReached(string task, string model)
        {
            GivenTheTaskHasItsOwnModel(task, model);
            _ai.UnavailableTasks.Add(Enum.Parse<AiTask>(task));
        }

        [Given("the settings give the AI task {string} the model {string}")]
        public void GivenTheSettingsGiveTheTaskTheModel(string task, string model)
        {
            var options = new AiOptions();
            options.Tasks[task] = new AiTaskOptions { Model = model };
            _configured = new ConfiguredAiModels(Options.Create(options), new NoHttp(), _ai, _ai);
        }

        [When("I open {string}")]
        public Task WhenIOpen(string path)
            => _web.OpenAsync(path);

        [Then("the rationale of {string} was written by {string}")]
        public async Task ThenTheRationaleWasWrittenBy(string code, string model)
        {
            await using var db = _web.CreateDbContext();
            var requirementIds = await db.Requirements.Where(r => r.Code == code).Select(r => r.Id).ToListAsync();
            var models = await db.MappingSuggestions.Where(s => requirementIds.Contains(s.RequirementId)).Select(s => s.RationaleModel).ToListAsync();
            Assert.Contains(model, models);
        }

        [Then("the AI task {string} uses {string} from its own setting")]
        public void ThenTheTaskUsesItsOwn(string task, string model)
            => Assert.Equal(new AiTaskModel(Enum.Parse<AiTask>(task), model, OwnSetting: true), Overview(task));

        [Then("the AI task {string} uses {string} by default")]
        public void ThenTheTaskUsesTheDefault(string task, string model)
            => Assert.Equal(new AiTaskModel(Enum.Parse<AiTask>(task), model, OwnSetting: false), Overview(task));

        [Then("the AI models list {string} with {string} from its own setting")]
        public void ThenTheAiModelsListOwn(string title, string model)
            => AssertRow(title, model, "Own setting");

        [Then("the AI models list {string} with {string} by default")]
        public void ThenTheAiModelsListDefault(string title, string model)
            => AssertRow(title, model, "Default");

        private AiTaskModel Overview(string task)
            => _configured!.Overview().Single(t => t.Task == Enum.Parse<AiTask>(task));

        private void AssertRow(string title, string model, string source)
        {
            var row = _web.Page!.QuerySelectorAll("#ai-models tbody tr").FirstOrDefault(r => r.QuerySelector("td")?.TextContent.Trim() == title)
                ?? throw new Xunit.Sdk.XunitException($"The System page lists no AI task '{title}'.");
            Assert.Equal(model, row.QuerySelector(".ai-model")?.TextContent.Trim());
            Assert.Equal(source, row.QuerySelector(".ai-model-source")?.TextContent.Trim());
        }

        // The settings scenario never calls a model
        private sealed class NoHttp : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new();
        }
    }
}
