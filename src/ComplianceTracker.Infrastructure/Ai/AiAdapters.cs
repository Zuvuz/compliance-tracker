using ComplianceTracker.Application.Ai;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Infrastructure.Ai
{
    // Adapters from IEmbeddingModel / IRationaleWriter to a model server (US-020).
    // Ai:Provider picks one: "Ollama" (its own API) or "OpenAICompatible" (vLLM, LM Studio, llama.cpp server,
    // Ollama's /v1 ...). A new kind of server is a new class here and one line in AddInfrastructure.
    // The HttpClient (base address, timeout, key) is set up in AddInfrastructure.

    // ---------------------------------------------------------- Ollama (https://github.com/ollama/ollama/blob/main/docs/api.md)

    public class OllamaEmbeddingModel : IEmbeddingModel
    {
        private readonly HttpClient _http;

        public OllamaEmbeddingModel(HttpClient http, IOptions<AiOptions> options)
        {
            _http = http;
            Name = options.Value.EmbeddingModel;
        }

        // US-117: a task's own model (ConfiguredAiModels)
        internal OllamaEmbeddingModel(HttpClient http, string model)
        {
            _http = http;
            Name = model;
        }

        public string Name { get; }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var answer = await AiHttp.PostAsync<EmbedAnswer>(_http, "api/embed", new { model = Name, input = texts }, cancellationToken);
            return AiHttp.Expect(answer.Embeddings, texts.Count, Name);
        }

        private sealed record EmbedAnswer([property: JsonPropertyName("embeddings")] List<float[]>? Embeddings);
    }

    public class OllamaRationaleWriter : IRationaleWriter
    {
        private readonly HttpClient _http;

        public OllamaRationaleWriter(HttpClient http, IOptions<AiOptions> options)
        {
            _http = http;
            Name = options.Value.ChatModel;
        }

        // US-117: a task's own model (ConfiguredAiModels)
        internal OllamaRationaleWriter(HttpClient http, string model)
        {
            _http = http;
            Name = model;
        }

        public string Name { get; }

        // Low temperature and a short answer: a rationale, not a story
        public async Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var answer = await AiHttp.PostAsync<GenerateAnswer>(_http, "api/generate",
                new { model = Name, prompt, stream = false, options = new { temperature = 0.2, num_predict = 120 } },
                cancellationToken);
            return answer.Response?.Trim() ?? "";
        }

        private sealed record GenerateAnswer([property: JsonPropertyName("response")] string? Response);
    }

    // ---------------------------------------------------------- OpenAI-compatible (/v1/embeddings, /v1/chat/completions)

    public class OpenAiCompatibleEmbeddingModel : IEmbeddingModel
    {
        private readonly HttpClient _http;

        public OpenAiCompatibleEmbeddingModel(HttpClient http, IOptions<AiOptions> options)
        {
            _http = http;
            Name = options.Value.EmbeddingModel;
        }

        // US-117: a task's own model (ConfiguredAiModels)
        internal OpenAiCompatibleEmbeddingModel(HttpClient http, string model)
        {
            _http = http;
            Name = model;
        }

        public string Name { get; }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var answer = await AiHttp.PostAsync<EmbeddingsAnswer>(_http, "v1/embeddings", new { model = Name, input = texts }, cancellationToken);
            var vectors = answer.Data?.OrderBy(d => d.Index).Select(d => d.Embedding).ToList();
            return AiHttp.Expect(vectors, texts.Count, Name);
        }

        private sealed record EmbeddingsAnswer([property: JsonPropertyName("data")] List<EmbeddingItem>? Data);
        private sealed record EmbeddingItem([property: JsonPropertyName("index")] int Index, [property: JsonPropertyName("embedding")] float[] Embedding);
    }

    public class OpenAiCompatibleRationaleWriter : IRationaleWriter
    {
        private readonly HttpClient _http;

        public OpenAiCompatibleRationaleWriter(HttpClient http, IOptions<AiOptions> options)
        {
            _http = http;
            Name = options.Value.ChatModel;
        }

        // US-117: a task's own model (ConfiguredAiModels)
        internal OpenAiCompatibleRationaleWriter(HttpClient http, string model)
        {
            _http = http;
            Name = model;
        }

        public string Name { get; }

        public async Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var answer = await AiHttp.PostAsync<ChatAnswer>(_http, "v1/chat/completions", new
            {
                model = Name,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.2,
                max_tokens = 120
            }, cancellationToken);
            return answer.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "";
        }

        private sealed record ChatAnswer([property: JsonPropertyName("choices")] List<Choice>? Choices);
        private sealed record Choice([property: JsonPropertyName("message")] ChatMessage? Message);
        private sealed record ChatMessage([property: JsonPropertyName("content")] string? Content);
    }

    // ---------------------------------------------------------- Shared

    internal static class AiHttp
    {
        // Every failure becomes AiUnavailableException with a message the user can act on
        public static async Task<T> PostAsync<T>(HttpClient http, string path, object body, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync(path, body, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new AiUnavailableException("The local AI is not available. Start Ollama (or the configured model server) and try again", error);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new AiUnavailableException(
                        $"The local AI answered with an error ({(int)response.StatusCode}). Is the model installed? {Short(detail)}");
                }

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                    ?? throw new AiUnavailableException("The local AI gave an empty answer");
            }
        }

        public static IReadOnlyList<float[]> Expect(List<float[]>? vectors, int count, string model)
            => vectors is not null && vectors.Count == count
                ? vectors
                : throw new AiUnavailableException($"The embedding model {model} did not return one vector per text");

        private static string Short(string text) => text.Length <= 200 ? text : text[..200];
    }
}
