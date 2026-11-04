using ComplianceTracker.Application.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Infrastructure.Ai
{
    // US-083 (B36): the AI providers as keyed services, chosen by the setting Ai:Provider (US-020).
    // The choice is configuration, not something a use case decides, so the factory lives here in Infrastructure.
    // Application keeps asking for IEmbeddingModel and IRationaleWriter and never sees which provider it gets.
    public static class AiProviders
    {
        public const string Ollama = "Ollama";
        public const string OpenAiCompatible = "OpenAICompatible";

        // R4: every provider the setting may name; anything else stops the app at start with a clear message
        public static readonly IReadOnlyList<string> Known = [Ollama, OpenAiCompatible];

        public static string UnknownMessage => $"Ai:Provider must be one of: {string.Join(", ", Known)}";

        // The key for the setting's value, without regard to case; null when it is unknown
        public static string? Key(string? provider)
            => Known.FirstOrDefault(k => string.Equals(k, provider?.Trim(), StringComparison.OrdinalIgnoreCase));

        // A new provider is one embedding model and one rationale writer, registered here under its name
        public static void Add(IServiceCollection services)
        {
            services.AddKeyedTransient<IEmbeddingModel>(Ollama, (provider, _) => provider.GetRequiredService<OllamaEmbeddingModel>());
            services.AddKeyedTransient<IRationaleWriter>(Ollama, (provider, _) => provider.GetRequiredService<OllamaRationaleWriter>());
            services.AddKeyedTransient<IEmbeddingModel>(OpenAiCompatible, (provider, _) => provider.GetRequiredService<OpenAiCompatibleEmbeddingModel>());
            services.AddKeyedTransient<IRationaleWriter>(OpenAiCompatible, (provider, _) => provider.GetRequiredService<OpenAiCompatibleRationaleWriter>());

            // The factory: the provider named in the settings
            services.AddTransient(provider => provider.GetRequiredKeyedService<IEmbeddingModel>(Chosen(provider)));
            services.AddTransient(provider => provider.GetRequiredKeyedService<IRationaleWriter>(Chosen(provider)));
        }

        private static string Chosen(IServiceProvider provider)
            => Key(provider.GetRequiredService<IOptions<AiOptions>>().Value.Provider) ?? throw new InvalidOperationException(UnknownMessage);
    }
}
