using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Ai
{
    // US-116: one measurement of an AI step against the decisions people made (approved and rejected suggestions of
    // the organisations that share them, US-036). Kept, so two runs on the same kind of test set can be compared.
    public class AiEvaluation
    {
        public Guid Id { get; } = Guid.NewGuid();
        public AiEvaluationStep Step { get; private set; }

        // The model and prompt version the run used (R2)
        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";

        // The test set: how many decisions, from when to when
        public int Decisions { get; private set; }
        public DateTime? DecisionsFrom { get; private set; }
        public DateTime? DecisionsTo { get; private set; }

        // Story matching (R3): share of approved matches in the top 1, 3 and 10, in per cent, and the share of rejected
        // matches that score above the limit
        public double? Top1 { get; private set; }
        public double? Top3 { get; private set; }
        public double? Top10 { get; private set; }
        public double? RejectedAboveLimit { get; private set; }

        // Rationales (R3): a person's grades from 1 to 5
        public int Graded { get; private set; }
        public double? AverageGrade { get; private set; }

        // The operator's name; the system admin is not a member of an organisation
        public string RunBy { get; private set; } = "";
        public DateTime RunAt { get; private set; }

        // For EF Core only
        private AiEvaluation() { }

        public static AiEvaluation ForMatching(
            string model, string promptVersion, int decisions, DateTime? from, DateTime? to,
            double top1, double top3, double top10, double rejectedAboveLimit, string runBy, DateTime at)
            => new()
            {
                Step = AiEvaluationStep.StoryMatching,
                Model = model,
                PromptVersion = promptVersion,
                Decisions = decisions,
                DecisionsFrom = from,
                DecisionsTo = to,
                Top1 = Percent(top1),
                Top3 = Percent(top3),
                Top10 = Percent(top10),
                RejectedAboveLimit = Percent(rejectedAboveLimit),
                RunBy = runBy,
                RunAt = at
            };

        public static AiEvaluation ForRationales(string model, string promptVersion, IReadOnlyCollection<int> grades, string runBy, DateTime at)
        {
            if (grades.Count == 0)
            {
                throw new DomainException("Grade at least one rationale");
            }

            if (grades.Any(g => g is < 1 or > 5))
            {
                throw new DomainException("A grade must be from 1 to 5");
            }

            return new AiEvaluation
            {
                Step = AiEvaluationStep.Rationale,
                Model = model,
                PromptVersion = promptVersion,
                Decisions = grades.Count,
                Graded = grades.Count,
                AverageGrade = Math.Round(grades.Average(), 2),
                RunBy = runBy,
                RunAt = at
            };
        }

        private static double Percent(double share) => Math.Round(100 * Math.Clamp(share, 0, 1), 1);
    }

    // Stored as text
    public enum AiEvaluationStep
    {
        StoryMatching,
        Rationale
    }
}
