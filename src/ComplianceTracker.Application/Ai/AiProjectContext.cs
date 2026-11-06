using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Ai
{
    // US-060, US-061: what the AI is told about the project for one requirement: the intended purpose and scope, the
    // project's classification under the requirement's regulation, and the open risks whose measures name the
    // requirement. It is only sent to the local AI (US-020 R2).
    public record AiProjectContext(
        string Purpose,
        string Scope,
        string? Classification,
        DateTime? ClassifiedAt,
        IReadOnlyList<(string Title, RiskLevel Level)> Risks)
    {
        // US-060 R4: an open High or Critical risk is tied to the requirement
        public bool HasHighRisk => Risks.Any(r => r.Level >= RiskLevel.High);

        // Shown with the suggestion, e.g. "purpose, scope, classification, risk 'Stolen sessions'"
        public string Used
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Purpose))
                {
                    parts.Add("purpose");
                }

                if (!string.IsNullOrWhiteSpace(Scope))
                {
                    parts.Add("scope");
                }

                if (Classification is not null)
                {
                    parts.Add("classification");
                }

                parts.AddRange(Risks.Select(r => $"risk '{r.Title}'"));
                return string.Join(", ", parts);
            }
        }

        // R1, R4 (US-061): empty parts are named as empty, so the AI does not guess them
        public string PromptText()
            => $"""
                About the product (use this, do not guess beyond it):
                Intended purpose: {(string.IsNullOrWhiteSpace(Purpose) ? "(not given)" : Purpose)}
                Scope: {(string.IsNullOrWhiteSpace(Scope) ? "(not given)" : Scope)}
                Classification under this regulation: {Classification ?? "(not classified)"}
                Open risks tied to this requirement: {(Risks.Count == 0 ? "(none)" : string.Join("; ", Risks.Select(r => $"{r.Title} ({r.Level})")))}
                """;
    }

    // Builds the context from the project, its risks and classifications. Used by the background rationale job and the
    // applicability suggestions, so it reads by project id (the callers check the organisation).
    public class AiContextBuilder
    {
        private readonly IRiskRepository _risks;
        private readonly IClassificationRepository _classifications;
        private readonly IFrameworkRepository _frameworks;

        public AiContextBuilder(IRiskRepository risks, IClassificationRepository classifications, IFrameworkRepository frameworks)
        {
            _risks = risks;
            _classifications = classifications;
            _frameworks = frameworks;
        }

        public async Task<AiProjectContext> ForAsync(Project project, Requirement requirement, CancellationToken cancellationToken = default)
        {
            // R3: open risks whose measures name the requirement; closed ones are left out
            var risks = (await _risks.ListAsync(project.Id, cancellationToken))
                .Where(r => r.Status != RiskStatus.Closed && r.Measures.Any(m => m.RequirementId == requirement.Id))
                .OrderByDescending(r => r.Score)
                .Select(r => (r.Title, r.Level))
                .ToList();

            // US-061 R1: the classification under the requirement's own regulation only
            string? classification = null;
            DateTime? classifiedAt = null;
            var used = project.Frameworks.FirstOrDefault(f => f.FrameworkVersionId == requirement.FrameworkVersionId);
            if (used is not null
                && await _frameworks.GetAvailableAsync(project.OrganizationId, used.FrameworkId, cancellationToken) is { } framework
                && RegulationClasses.For(framework) is { } regulation
                && (await _classifications.ListAsync(project.Id, cancellationToken))
                    .Where(c => c.FrameworkId == framework.Id).OrderBy(c => c.ClassifiedAt).LastOrDefault() is { } current)
            {
                classifiedAt = current.ClassifiedAt;
                classification = $"{regulation.TitleOf(current.Class)} (decided {current.ClassifiedAt:dd-MM-yyyy}: {current.Reason})";
            }

            return new AiProjectContext(project.IntendedPurpose, project.Scope, classification, classifiedAt, risks);
        }
    }
}
