using ComplianceTracker.Application.Ai;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Infrastructure.Ai
{
    // US-105: the limits from the settings (Ai:MinimumScoreForStories, Ai:MinimumScoreForEvidence), between 0 and 1
    public class ConfiguredAiLimits : IAiLimits
    {
        private readonly IOptionsMonitor<AiOptions> _options;

        public ConfiguredAiLimits(IOptionsMonitor<AiOptions> options)
        {
            _options = options;
        }

        public double MinimumScoreForStories => Math.Clamp(_options.CurrentValue.MinimumScoreForStories, 0, 1);

        public double MinimumScoreForEvidence => Math.Clamp(_options.CurrentValue.MinimumScoreForEvidence, 0, 1);

        public int MinimumDecisionsForEvaluation => Math.Max(1, _options.CurrentValue.MinimumDecisionsForEvaluation);
    }
}
