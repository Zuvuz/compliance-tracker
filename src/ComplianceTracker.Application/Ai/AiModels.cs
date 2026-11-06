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

    // US-105: how good a suggestion must be before a person sees it (R1, R2). The operator sets them (Ai:*), and every
    // suggestion keeps the limit it was made with (R5).
    public interface IAiLimits
    {
        // US-020, US-044: user story <-> requirement
        double MinimumScoreForStories { get; }

        // US-059: evidence <-> requirement
        double MinimumScoreForEvidence { get; }
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
        // 127.0.0.1, not localhost: on Windows localhost may resolve to ::1 first, where another Ollama (e.g. in Docker) can answer.
        public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

        public string EmbeddingModel { get; set; } = "nomic-embed-text";
        public string ChatModel { get; set; } = "qwen2.5:3b";

        // Only for servers that need one (OpenAICompatible); never in the repo, e.g. an environment variable
        public string? ApiKey { get; set; }

        // A small model on a laptop CPU can take a while per rationale
        public int TimeoutSeconds { get; set; } = 120;

        // US-105 R1, R2: suggestions below these cosine scores are not made (B47). A starting point, to be tuned from
        // the scores of approved and rejected suggestions (US-036 export).
        public double MinimumScoreForStories { get; set; } = 0.5;
        public double MinimumScoreForEvidence { get; set; } = 0.5;
    }

    // The prompt for a rationale. Its version is stored with every suggestion, so results from different prompts
    // can be told apart (US-036 R3). Change the version whenever the text changes.
    public static class RationalePrompt
    {
        // mapping-v1 (US-020); mapping-v3 adds the project's purpose, scope, risks (US-060) and classification (US-061)
        public const string Version = "mapping-v3";

        public static string For(string storyTitle, string storyText, string requirementCode, string requirementText, AiProjectContext? context = null)
            => $"""
                You help a software team map their user stories to regulatory requirements.
                In one or two short sentences, explain how the user story contributes to the requirement,
                or say clearly that it does not. Use only the texts below. Do not invent facts.

                Requirement {requirementCode}:
                {requirementText}

                User story "{storyTitle}":
                {storyText}
                {(context is null ? "" : "\n" + context.PromptText() + "\n")}
                Rationale:
                """;
    }
}
