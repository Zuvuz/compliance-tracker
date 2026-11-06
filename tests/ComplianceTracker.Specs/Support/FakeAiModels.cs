using ComplianceTracker.Application.Ai;

namespace ComplianceTracker.Specs.Support
{
    // US-117: the pretend AI as the model of every task. A scenario can give a task its own model name
    // (FakeAi.TaskModels) or make a task's model unreachable (FakeAi.UnavailableTasks); the answers stay the pretend AI's.
    public sealed class FakeAiModels : IAiModels
    {
        private readonly FakeAi _ai;

        public FakeAiModels(FakeAi ai)
        {
            _ai = ai;
        }

        public IEmbeddingModel Embedding(AiTask task) => For(task);

        public IRationaleWriter Writer(AiTask task) => For(task);

        public IReadOnlyList<AiTaskModel> Overview()
            => Enum.GetValues<AiTask>()
                .Select(task => _ai.TaskModels.TryGetValue(task, out var name)
                    ? new AiTaskModel(task, name, OwnSetting: true)
                    : new AiTaskModel(task, AiTaskNames.IsEmbedding(task) ? ((IEmbeddingModel)_ai).Name : ((IRationaleWriter)_ai).Name, OwnSetting: false))
                .ToList();

        private TaskModel For(AiTask task)
            => new(_ai, task, _ai.TaskModels.GetValueOrDefault(task));

        // The pretend AI under the task's own name, failing like ConfiguredAiModels when the task's model is down
        private sealed class TaskModel : IEmbeddingModel, IRationaleWriter
        {
            private readonly FakeAi _ai;
            private readonly AiTask _task;
            private readonly string? _name;

            public TaskModel(FakeAi ai, AiTask task, string? name)
            {
                _ai = ai;
                _task = task;
                _name = name;
            }

            string IEmbeddingModel.Name => _name ?? ((IEmbeddingModel)_ai).Name;
            string IRationaleWriter.Name => _name ?? ((IRationaleWriter)_ai).Name;

            public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            {
                ThrowIfUnavailable();
                return _ai.EmbedAsync(texts, cancellationToken);
            }

            public Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                ThrowIfUnavailable();
                return _ai.WriteAsync(prompt, cancellationToken);
            }

            private void ThrowIfUnavailable()
            {
                if (_ai.UnavailableTasks.Contains(_task))
                {
                    throw new AiUnavailableException($"{AiTaskNames.Title(_task)} is unavailable: the model {_name ?? "(default)"} cannot be reached");
                }
            }
        }
    }
}
