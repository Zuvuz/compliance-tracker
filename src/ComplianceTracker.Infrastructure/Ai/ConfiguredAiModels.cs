using ComplianceTracker.Application.Ai;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Infrastructure.Ai
{
    // US-117: the model of each AI task, from Ai:Tasks:<Task>. A task without its own setting gets the default model
    // (Ai:EmbeddingModel / Ai:ChatModel); one with its own gets an adapter of the configured provider for that model,
    // on its own server when Ai:Tasks:<Task>:BaseUrl is set. A task whose own model cannot be reached says so (R5).
    public class ConfiguredAiModels : IAiModels
    {
        public const string HttpClientName = "ai-task";

        private readonly AiOptions _options;
        private readonly IHttpClientFactory _http;
        private readonly IEmbeddingModel _defaultEmbedding;
        private readonly IRationaleWriter _defaultWriter;

        public ConfiguredAiModels(IOptions<AiOptions> options, IHttpClientFactory http, IEmbeddingModel defaultEmbedding, IRationaleWriter defaultWriter)
        {
            _options = options.Value;
            _http = http;
            _defaultEmbedding = defaultEmbedding;
            _defaultWriter = defaultWriter;
        }

        public IEmbeddingModel Embedding(AiTask task)
        {
            if (Own(task) is not { } own)
            {
                return _defaultEmbedding;
            }

            var client = Client(own);
            IEmbeddingModel model = AiProviders.Key(_options.Provider) == AiProviders.OpenAiCompatible
                ? new OpenAiCompatibleEmbeddingModel(client, own.Model!)
                : new OllamaEmbeddingModel(client, own.Model!);
            return new TaskEmbeddingModel(task, model);
        }

        public IRationaleWriter Writer(AiTask task)
        {
            if (Own(task) is not { } own)
            {
                return _defaultWriter;
            }

            var client = Client(own);
            IRationaleWriter writer = AiProviders.Key(_options.Provider) == AiProviders.OpenAiCompatible
                ? new OpenAiCompatibleRationaleWriter(client, own.Model!)
                : new OllamaRationaleWriter(client, own.Model!);
            return new TaskRationaleWriter(task, writer);
        }

        public IReadOnlyList<AiTaskModel> Overview()
            => Enum.GetValues<AiTask>()
                .Select(task => Own(task) is { } own
                    ? new AiTaskModel(task, own.Model!, OwnSetting: true)
                    : new AiTaskModel(task, AiTaskNames.IsEmbedding(task) ? _defaultEmbedding.Name : _defaultWriter.Name, OwnSetting: false))
                .ToList();

        // The task's own setting, when it names a model; the section's key is matched without regard to case
        private AiTaskOptions? Own(AiTask task)
            => _options.Tasks.FirstOrDefault(t => string.Equals(t.Key, task.ToString(), StringComparison.OrdinalIgnoreCase)).Value is { } own
               && !string.IsNullOrWhiteSpace(own.Model)
                ? own
                : null;

        // Like the default clients (AddInfrastructure), on the task's own server when it has one
        private HttpClient Client(AiTaskOptions own)
        {
            var client = _http.CreateClient(HttpClientName);
            client.BaseAddress = new Uri((own.BaseUrl ?? _options.BaseUrl).TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            return client;
        }
    }

    // R5: a task's own model that cannot be reached is named, so it is clear that the other tasks still work
    internal sealed class TaskRationaleWriter : IRationaleWriter
    {
        private readonly AiTask _task;
        private readonly IRationaleWriter _inner;

        public TaskRationaleWriter(AiTask task, IRationaleWriter inner)
        {
            _task = task;
            _inner = inner;
        }

        public string Name => _inner.Name;

        public async Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _inner.WriteAsync(prompt, cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                throw new AiUnavailableException($"{AiTaskNames.Title(_task)} is unavailable: the model {Name} cannot be reached", error);
            }
        }
    }

    internal sealed class TaskEmbeddingModel : IEmbeddingModel
    {
        private readonly AiTask _task;
        private readonly IEmbeddingModel _inner;

        public TaskEmbeddingModel(AiTask task, IEmbeddingModel inner)
        {
            _task = task;
            _inner = inner;
        }

        public string Name => _inner.Name;

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _inner.EmbedAsync(texts, cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                throw new AiUnavailableException($"{AiTaskNames.Title(_task)} is unavailable: the model {Name} cannot be reached", error);
            }
        }
    }
}
