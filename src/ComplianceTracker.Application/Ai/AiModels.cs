namespace ComplianceTracker.Application.Ai
{
    // The tracker's view of an AI model (US-020). Nothing outside the adapters in Infrastructure knows which
    // model or which server runs it, so a better model is a setting (Ai:*), and a new kind of server is a new adapter.

    // Turns texts into vectors; similar texts get similar vectors. Used to find candidate requirements for a story.
    public interface IEmbeddingModel
    {
        // The model's name as stored with every suggestion, e.g. "nomic-embed-text"
        string Name { get; }

        // One vector per text, in the same order. Throws AiUnavailableException when the model cannot be reached.
        Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    }

    // Writes the short rationale for one suggestion
    public interface IRationaleWriter
    {
        // The model's name as stored with every rationale, e.g. "qwen2.5:3b"
        string Name { get; }

        Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default);
    }

    // The local AI cannot be reached or answered with an error. The message is shown to the user.
    public class AiUnavailableException : Exception
    {
        public AiUnavailableException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    // Settings in the section "Ai". Changing the model or the server needs no code change.
    public class AiOptions
    {
        public const string Section = "Ai";

        // "Ollama" (its own API) or "OpenAICompatible" (vLLM, LM Studio, llama.cpp server, Ollama's /v1 ...)
        public string Provider { get; set; } = "Ollama";

        // Where the model server runs. US-020 R2: it must be inside the organisation's own infrastructure.
        public string BaseUrl { get; set; } = "http://localhost:11434";

        public string EmbeddingModel { get; set; } = "nomic-embed-text";
        public string ChatModel { get; set; } = "qwen2.5:3b";

        // Only for servers that need one (OpenAICompatible); never in the repo, e.g. an environment variable
        public string? ApiKey { get; set; }

        // A small model on a laptop CPU can take a while per rationale
        public int TimeoutSeconds { get; set; } = 120;
    }

    // The prompt for a rationale. Its version is stored with every suggestion, so results from different prompts
    // can be told apart (US-036 R3). Change the version whenever the text changes.
    public static class RationalePrompt
    {
        public const string Version = "mapping-v1";

        public static string For(string storyTitle, string storyText, string requirementCode, string requirementText)
            => $"""
                You help a software team map their user stories to regulatory requirements.
                In one or two short sentences, explain how the user story contributes to the requirement,
                or say clearly that it does not. Use only the texts below. Do not invent facts.

                Requirement {requirementCode}:
                {requirementText}

                User story "{storyTitle}":
                {storyText}

                Rationale:
                """;
    }
}
