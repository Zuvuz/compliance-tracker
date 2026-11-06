using ComplianceTracker.Application.Ai;
using ComplianceTracker.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.UnitTests.Ai;

// Edge cases of the model per task (US-117): which settings count, and the defaults. No model is called.
public class ConfiguredAiModelsTests
{
    private sealed class Default : IEmbeddingModel, IRationaleWriter
    {
        string IEmbeddingModel.Name => "default-embed";
        string IRationaleWriter.Name => "default-chat";
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static ConfiguredAiModels Models(Action<AiOptions> configure)
    {
        var options = new AiOptions();
        configure(options);
        var model = new Default();
        return new ConfiguredAiModels(Options.Create(options), new NoHttp(), model, model);
    }

    [Fact]
    public void Without_settings_every_task_uses_the_default()
    {
        // Arrange
        var models = Models(_ => { });

        // Act
        var overview = models.Overview();

        // Assert
        Assert.Equal(Enum.GetValues<AiTask>().Length, overview.Count);
        Assert.All(overview, t => Assert.False(t.OwnSetting));
        Assert.Equal("default-embed", overview.Single(t => t.Task == AiTask.Embedding).Model);
        Assert.Equal("default-chat", overview.Single(t => t.Task == AiTask.Rationale).Model);
    }

    [Theory]
    [InlineData("Rationale")]
    [InlineData("rationale")]
    [InlineData("RATIONALE")]
    public void The_task_name_in_the_settings_is_matched_without_regard_to_case(string key)
    {
        // Arrange
        var models = Models(o => o.Tasks[key] = new AiTaskOptions { Model = "large-chat" });

        // Act
        var writer = models.Writer(AiTask.Rationale);

        // Assert
        Assert.Equal("large-chat", writer.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_task_setting_without_a_model_uses_the_default(string? model)
    {
        // Arrange
        var models = Models(o => o.Tasks["Rationale"] = new AiTaskOptions { Model = model, BaseUrl = "http://10.0.0.5:11434" });

        // Act
        var writer = models.Writer(AiTask.Rationale);

        // Assert
        Assert.Equal("default-chat", writer.Name);
    }

    [Fact]
    public void A_setting_for_an_unknown_task_changes_nothing()
    {
        // Arrange
        var models = Models(o => o.Tasks["NoSuchTask"] = new AiTaskOptions { Model = "large-chat" });

        // Act
        var overview = models.Overview();

        // Assert
        Assert.All(overview, t => Assert.False(t.OwnSetting));
    }

    [Fact]
    public void One_task_with_its_own_model_leaves_the_others_on_the_default()
    {
        // Arrange
        var models = Models(o => o.Tasks["Embedding"] = new AiTaskOptions { Model = "big-embed" });

        // Act
        var embedding = models.Embedding(AiTask.Embedding);
        var rationale = models.Writer(AiTask.Rationale);

        // Assert
        Assert.Equal("big-embed", embedding.Name);
        Assert.Equal("default-chat", rationale.Name);
    }
}
