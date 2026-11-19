using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Web.Filters;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // The fields of the add/edit risk form (US-042 R1, R3). The domain checks the values.
    // IFormModel: on a broken rule, DomainExceptionFilter shows the form again with what was typed.
    public class RiskFormModel : IFormModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        // Empty when adding
        public Guid? RiskId { get; set; }

        // US-092: the version the edit form was opened with
        public uint? Version { get; set; }

        [Display(Name = "Title")]
        public string? Title { get; set; }

        [Display(Name = "Threat and what it affects")]
        public string? Description { get; set; }

        [Display(Name = "Likelihood (1–5)")]
        public int Likelihood { get; set; } = 3;

        [Display(Name = "Impact (1–5)")]
        public int Impact { get; set; } = 3;

        [Display(Name = "Owner")]
        public Guid OwnerId { get; set; }

        [Display(Name = "Treatment")]
        public RiskTreatment Treatment { get; set; } = RiskTreatment.Reduce;

        [Display(Name = "Residual likelihood (optional)")]
        public int? ResidualLikelihood { get; set; }

        [Display(Name = "Residual impact (optional)")]
        public int? ResidualImpact { get; set; }

        public static RiskFormModel From(Risk risk, string projectName) => new()
        {
            ProjectId = risk.ProjectId,
            ProjectName = projectName,
            RiskId = risk.Id,
            Version = risk.Version,
            Title = risk.Title,
            Description = risk.Description,
            Likelihood = risk.Likelihood,
            Impact = risk.Impact,
            OwnerId = risk.OwnerId,
            Treatment = risk.Treatment,
            ResidualLikelihood = risk.ResidualLikelihood,
            ResidualImpact = risk.ResidualImpact
        };
    }

    // #52: one measure on the risk's page: the requirement's page it leads to (Sub and Code), or the requirements a story
    // covers; what stands behind it in words, e.g. "1 story · 0 evidence"; and whether evidence is missing
    public record MeasureView(string? Sub, string? Code, IReadOnlyList<(string Sub, string Code)> StoryRequirements, string Behind,
        bool NeedsEvidence = false)
    {
        public static string Counts(int stories, int evidence)
            => $"{stories} {(stories == 1 ? "story" : "stories")} · {evidence} evidence";
    }

    // The risk's own page: the risk, its project, names, and what can be linked as measures (US-042 R5)
    public record RiskPageViewModel(
        Risk Risk,
        Domain.Projects.Project Project,
        Domain.Organizations.Organization Organization,
        Application.Compliance.ProjectCompliance? Compliance,
        DateTime Now)
    {
        public string NameOf(Guid? userId)
            => Organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";

        // US-203 R4: the risk's history, newest first
        public IReadOnlyList<Application.Risks.RiskHistoryEntry> History { get; init; } = [];

        // US-106 R12: the AI's pending measure suggestions for this risk, best first
        public IReadOnlyList<Domain.Risks.RiskMeasureSuggestion> MeasureSuggestions { get; init; } = [];

        // US-161: the AI's suggested evidence per suggested measure
        public Application.Risks.MeasureEvidenceView MeasureEvidence { get; init; } = Application.Risks.MeasureEvidenceView.None;

        // The name of a suggested measure: the requirement's code and title, or the story's title
        public string MeasureName(Domain.Risks.RiskMeasureSuggestion suggestion)
            => suggestion.RequirementId is { } id
                ? Compliance?.Frameworks.SelectMany(f => f.Requirements).FirstOrDefault(r => r.Requirement.Id == id) is { } row
                    ? $"{row.Requirement.Code} {row.Requirement.Title}" : "?"
                : Compliance?.WorkItems.FirstOrDefault(w => w.Id == suggestion.WorkItemId)?.Title ?? "?";

        // US-090 R5: a removed owner is shown as such until a new owner is chosen
        public string OwnerOf(Guid userId)
            => OwnerName(Organization, userId);

        public static string OwnerName(Domain.Organizations.Organization? organization, Guid userId)
            => organization?.Users.FirstOrDefault(u => u.Id == userId) is { } owner
                ? owner.IsActive ? owner.Name : $"Owner removed ({owner.Name})"
                : "Unknown";

        // US-111 R2: the project's evidence, to link to an action
        public IReadOnlyList<Domain.Mappings.Evidence> ProjectEvidence { get; init; } = [];

        public DateOnly Today => DateOnly.FromDateTime(Now);

        // #52: where a measure leads on to, and what stands behind it. A requirement: its page (sub-tab and code) with its
        // stories and evidence. A story: the requirements it covers (each with its page) and its evidence.
        public MeasureView ViewOf(RiskMeasure measure)
        {
            if (Compliance is null)
            {
                return new MeasureView(null, null, [], "");
            }

            if (measure.RequirementId is { } id)
            {
                foreach (var framework in Compliance.Frameworks)
                {
                    if (framework.Requirements.FirstOrDefault(r => r.Requirement.Id == id) is { } row)
                    {
                        var stories = row.Links.Count(l => l.WorkItem.State != Domain.WorkItems.WorkItemState.Removed);
                        var evidence = row.Evidence.Count(e => !e.IsExpired);
                        return new MeasureView(FrameworkNames.Slug(framework.Framework.Name), row.Requirement.Code, [],
                            MeasureView.Counts(stories, evidence), evidence == 0 && row.IsApplicable);
                    }
                }

                return new MeasureView(null, null, [], "");
            }

            var storyId = measure.WorkItemId!.Value;
            var requirements = Compliance.Frameworks
                .SelectMany(f => f.Requirements.Where(r => r.Links.Any(l => l.WorkItem.Id == storyId))
                    .Select(r => (Sub: FrameworkNames.Slug(f.Framework.Name), r.Requirement.Code)))
                .Distinct()
                .ToList();
            var storyEvidence = Compliance.EvidenceOf(storyId).Count;
            return new MeasureView(null, null, requirements,
                $"{(requirements.Count == 0 ? "no requirement linked" : $"covers {string.Join(", ", requirements.Select(r => r.Code))}")} · {storyEvidence} evidence",
                storyEvidence == 0);
        }

        public Application.Risks.RiskTrace? Trace => Compliance is null ? null : Application.Risks.RiskTraces.For(Risk, Compliance);

        public Domain.WorkItems.WorkItem? WorkItemOf(Guid? workItemId)
            => Compliance?.WorkItems.FirstOrDefault(w => w.Id == workItemId);

        public string EvidenceTitle(Guid? evidenceId)
            => ProjectEvidence.FirstOrDefault(e => e.Id == evidenceId)?.Title ?? "(evidence removed)";

        // US-202 R7: why a risk that is not reduced needs no actions
        public static string TreatedAs(RiskTreatment treatment) => treatment switch
        {
            RiskTreatment.Accept => "accepted",
            RiskTreatment.Avoid => "avoided",
            RiskTreatment.Transfer => "transferred",
            _ => "reduced"
        };

        // US-202 R4: up to five applicable requirements, not yet measures, that share the most words with the risk's title and
        // description, best first. Short and common words do not count.
        public IReadOnlyList<Application.Compliance.RequirementCoverage> RelevantRequirements(int max = 5)
        {
            var words = Words($"{Risk.Title} {Risk.Description}");
            if (Compliance is null || words.Count == 0)
            {
                return [];
            }

            return Compliance.Frameworks.SelectMany(f => f.Requirements)
                .Where(r => r.IsApplicable && Risk.Measures.All(m => m.RequirementId != r.Requirement.Id))
                .Select(r => (Row: r, Score: Words($"{r.Requirement.Title} {r.Requirement.Description}").Count(words.Contains)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score).ThenBy(x => x.Row.Requirement.Code, StringComparer.Ordinal)
                .Take(max)
                .Select(x => x.Row)
                .ToList();
        }

        private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "that", "this", "with", "from", "have", "been", "into", "their", "they", "them", "when", "which", "where", "shall",
            "should", "could", "would", "about", "other", "there", "these", "those", "than", "then", "also", "such", "only",
            "must", "will", "each", "every", "after", "before", "under", "over", "more", "most", "some", "user", "users", "system"
        };

        private static HashSet<string> Words(string text)
            => new(System.Text.RegularExpressions.Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+")
                .Where(w => w.Length >= 4 && !CommonWords.Contains(w))
                .Select(w => w.Length > 5 && w.EndsWith('s') ? w[..^1] : w), StringComparer.Ordinal);

        public static string StatusText(RiskActionStatus status) => status switch
        {
            RiskActionStatus.InProgress => "In progress",
            _ => status.ToString()
        };

        public string MeasureName(RiskMeasure measure)
            => measure.RequirementId is { } requirementId
                ? Compliance?.Frameworks.SelectMany(f => f.Requirements).FirstOrDefault(r => r.Requirement.Id == requirementId) is { } row
                    ? $"{row.Requirement.Code} {row.Requirement.Title}"
                    : "(requirement no longer followed)"
                : Compliance?.WorkItems.FirstOrDefault(w => w.Id == measure.WorkItemId)?.Title ?? "(user story removed)";
    }
}
