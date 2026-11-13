using ComplianceTracker.Application.Ai;

namespace ComplianceTracker.Specs.Support
{
    // A pretend local AI for one scenario (US-020, US-036), so the tests need no model and give the same result every time.
    // The scenario sets how similar a story and a requirement are; the vectors are made so the cosine similarity
    // is exactly that score: each requirement code is its own axis, and a story points along the axes of its scores.
    // Reqnroll creates one per scenario and gives the same instance to WebScenario and the steps.
    // US-105: it also holds the limits, 0 unless a scenario sets them, so older scenarios see every suggestion
    public sealed class FakeAi : IEmbeddingModel, IRationaleWriter, IAiLimits
    {
        public double MinimumScoreForStories { get; set; }
        public double MinimumScoreForEvidence { get; set; }

        // US-116: 1 unless a scenario sets it, so a few decisions are enough to measure
        public int MinimumDecisionsForEvaluation { get; set; } = 1;

        private const int Dimensions = 256;

        private readonly Dictionary<(string Story, string Code), double> _scores = new();
        private readonly Dictionary<string, int> _axes = new(StringComparer.Ordinal);

        // Makes every call fail, as when Ollama is not running
        public bool Unavailable { get; set; }

        public int RationalesWritten { get; private set; }

        // US-060: the requirements the AI answers "does not apply" for, with the reason; every other answer is "APPLIES"
        public Dictionary<string, string> NotApplicable { get; } = new(StringComparer.Ordinal);

        // US-038: the answer to "Check whether the evidence …" per requirement code; "Covers" when none is set
        public Dictionary<string, string> EvidenceAnswers { get; } = new(StringComparer.Ordinal);

        // US-060, US-061: every prompt, so a scenario can check what the AI was told
        public List<string> Prompts { get; } = new();
        // US-117: tasks with their own model name, and tasks whose own model cannot be reached (FakeAiModels)
        public Dictionary<AiTask, string> TaskModels { get; } = new();
        public HashSet<AiTask> UnavailableTasks { get; } = new();

        string IEmbeddingModel.Name => "fake-embed";
        string IRationaleWriter.Name => "fake-chat";

        public void Rate(string storyTitle, string code, double score)
        {
            lock (this)
            {
                _scores[(storyTitle, code)] = score;
                Axis(code);
            }
        }

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            ThrowIfUnavailable();
            lock (this)
            {
                return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Vector).ToList());
            }
        }

        // "Fake rationale for SP-1": the code is read from the prompt (RationalePrompt)
        public Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            ThrowIfUnavailable();
            var start = prompt.IndexOf("Requirement ", StringComparison.Ordinal) + "Requirement ".Length;
            var code = prompt[start..prompt.IndexOf(':', start)];
            lock (this)
            {
                Prompts.Add(prompt);
                if (prompt.Contains(ApplicabilityPrompt.Marker, StringComparison.Ordinal))
                {
                    return Task.FromResult(NotApplicable.TryGetValue(code, out var reason) ? $"NOT APPLICABLE: {reason}" : "APPLIES");
                }

                // US-038: the evidence check's labelled answer
                if (prompt.Contains(ComplianceTracker.Application.Compliance.EvidenceCheckPrompt.Marker, StringComparison.Ordinal))
                {
                    return Task.FromResult(EvidenceAnswers.TryGetValue(code, out var answer)
                        ? answer
                        : "VERDICT: Covers\nMISSING: Nothing\nQUOTES: The evidence shows it");
                }

                // US-106: a draft user story in the labelled shape the prompt asks for
                if (prompt.Contains(StoryDraftPrompt.Marker, StringComparison.Ordinal))
                {
                    return Task.FromResult(
                        $"TITLE: Meet {code}\nROLE: User\nSTORY: As a user, I want {code} met, so that the product is compliant.\nRULES: R1: {code} is met\nEXAMPLES: A test shows {code}");
                }

                RationalesWritten++;
            }

            return Task.FromResult($"Fake rationale for {code}");
        }

        // A requirement's text starts with its code (MappingSuggestionService.RequirementText); a story's with its title
        private float[] Vector(string text)
        {
            var vector = new float[Dimensions];
            var firstLine = text.Split('\n')[0];
            // The code is the first word, or a longer rated code with spaces in it, e.g. "Art. 21(2)(b)" (US-136 R2)
            var code = _axes.Keys.Where(k => k.Contains(' ') && firstLine.StartsWith(k + " ", StringComparison.Ordinal))
                .OrderByDescending(k => k.Length).FirstOrDefault() ?? firstLine.Split(' ')[0];

            var storyScores = _scores.Where(s => s.Key.Story == firstLine).ToList();
            if (storyScores.Count > 0)
            {
                foreach (var ((_, scoredCode), score) in storyScores)
                {
                    vector[Axis(scoredCode)] = (float)score;
                }

                // The rest of the length goes to an axis no requirement uses, so the cosine equals the score
                var sum = storyScores.Sum(s => s.Value * s.Value);
                vector[Dimensions - 1] = (float)Math.Sqrt(Math.Max(0, 1 - sum));
                return vector;
            }

            vector[Axis(code)] = 1;
            return vector;
        }

        private int Axis(string code)
        {
            if (!_axes.TryGetValue(code, out var axis))
            {
                axis = _axes.Count;
                _axes[code] = axis;
            }

            return axis;
        }

        private void ThrowIfUnavailable()
        {
            if (Unavailable)
            {
                throw new AiUnavailableException("The local AI is not available. Start Ollama (or the configured model server) and try again");
            }
        }
    }
}
