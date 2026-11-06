namespace ComplianceTracker.Application.Ai
{
    // US-117: what the AI is used for. Each task can have its own model (Ai:Tasks:<Task>:Model, and :BaseUrl when it
    // runs on another server inside the organisation), so the many small calls stay fast while the few that write text
    // for people can use a larger model. A task without its own setting uses the default model.
    public enum AiTask
    {
        // Finding candidate requirements for a story or evidence (vectors)
        Embedding,

        // Reranking the candidates (US-118)
        Ranking,

        // The rationale of a story suggestion (US-020)
        Rationale,

        // The rationale of an evidence suggestion (US-059), and later the evidence check (US-038)
        EvidenceCheck,

        // Drafts for the gaps (US-108, US-119)
        GapDrafts,

        // Risk drafts (US-112)
        RiskDrafts,

        // Filling in the import templates (US-114)
        ImportFill
    }

    // US-117: the model of each task (implemented in Infrastructure from the settings)
    public interface IAiModels
    {
        IEmbeddingModel Embedding(AiTask task);

        IRationaleWriter Writer(AiTask task);

        // R1, for the operator: which model each task uses, and whether it has its own setting
        IReadOnlyList<AiTaskModel> Overview();
    }

    public record AiTaskModel(AiTask Task, string Model, bool OwnSetting);

    // US-117: one task's own model in the settings section "Ai:Tasks:<Task>"
    public class AiTaskOptions
    {
        public string? Model { get; set; }

        // Only when the task runs on another server than Ai:BaseUrl; still inside the organisation (US-020 R2)
        public string? BaseUrl { get; set; }
    }

    public static class AiTaskNames
    {
        // e.g. "Gap drafts"
        public static string Title(AiTask task) => task switch
        {
            AiTask.EvidenceCheck => "Evidence check",
            AiTask.GapDrafts => "Gap drafts",
            AiTask.RiskDrafts => "Risk drafts",
            AiTask.ImportFill => "Import fill",
            _ => task.ToString()
        };

        // The tasks that use vectors; the rest write text
        public static bool IsEmbedding(AiTask task) => task == AiTask.Embedding;
    }
}
