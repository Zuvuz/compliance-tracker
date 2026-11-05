using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Compliance
{
    // What the Compliance tab reads (US-019, US-021, US-041): per framework the project follows,
    // the requirements of the version it uses, the user stories linked to each, and the coverage.
    // Only the signed-in user's own projects; others give null.
    public class ComplianceQueries
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceSuggestionRepository _evidenceSuggestions;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public ComplianceQueries(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IMappingSuggestionRepository suggestions,
            IEvidenceRepository evidence,
            IEvidenceSuggestionRepository evidenceSuggestions,
            CurrentMember member,
            TimeProvider clock)
        {
            _evidenceSuggestions = evidenceSuggestions;
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _applicability = applicability;
            _suggestions = suggestions;
            _evidence = evidence;
            _member = member;
            _clock = clock;
        }

        // US-057 R6: a file of the member's own project, for anyone in the organisation; null otherwise
        public async Task<(Domain.Mappings.Evidence Evidence, byte[] Content)?> EvidenceFileAsync(
            Guid projectId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || await _evidence.GetAsync(project.Id, evidenceId, cancellationToken) is not { IsFile: true } evidence
                || await _evidence.GetContentAsync(evidence.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (evidence, content);
        }

        public async Task<ProjectCompliance?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(
                organization.Id, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var workItems = (await _workItems.ListAsync(project.Id, cancellationToken)).ToDictionary(w => w.Id);
            var links = await _links.ListActiveAsync(project.Id, cancellationToken);
            var notApplicable = (await _applicability.ListActiveAsync(project.Id, cancellationToken))
                .ToDictionary(a => a.RequirementId, a => a.Reason);

            // US-020, US-044 R4: the pending AI suggestions, for stories that still exist
            var pendingSuggestions = (await _suggestions.ListAsync(project.Id, cancellationToken))
                .Where(s => s.IsPending && workItems.ContainsKey(s.WorkItemId))
                .ToList();

            // US-057: the evidence that is not removed, newest first; expired evidence is shown but does not count
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            // US-059 R6: counted through the links
            var evidence = (await _evidence.ListActiveAsync(project.Id, cancellationToken))
                .OrderByDescending(e => e.AddedAt)
                .ToList();
            var evidenceLinks = (await _evidence.ListActiveLinksAsync(project.Id, cancellationToken))
                .ToLookup(l => l.RequirementId, l => l.EvidenceId);

            // US-058: suggestions from tests and pull requests that wait for a person, and the newest run's failing tests
            var evidenceSuggestions = (await _evidenceSuggestions.ListAsync(project.Id, cancellationToken))
                .Where(s => s.Status is Domain.Mappings.EvidenceSuggestionStatus.Pending or Domain.Mappings.EvidenceSuggestionStatus.Blocked)
                .OrderByDescending(s => s.CreatedAt)
                .ToLookup(s => s.RequirementId);

            var result = new List<FrameworkCompliance>();
            foreach (var used in project.Frameworks)
            {
                var framework = frameworks.FirstOrDefault(f => f.Id == used.FrameworkId);
                var version = framework?.Versions.FirstOrDefault(v => v.Id == used.FrameworkVersionId);
                if (framework is null || version is null)
                {
                    continue;
                }

                var rows = version.Requirements
                    .Select(requirement => new RequirementCoverage(
                        requirement,
                        links.Where(l => l.RequirementId == requirement.Id && workItems.ContainsKey(l.WorkItemId))
                            .Select(l => new LinkedWorkItem(l, workItems[l.WorkItemId]))
                            .OrderBy(l => l.WorkItem.Title, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        notApplicable.GetValueOrDefault(requirement.Id))
                    {
                        // US-044 R4: best first
                        Suggestions = pendingSuggestions.Where(s => s.RequirementId == requirement.Id)
                            .OrderByDescending(s => s.Score)
                            .Select(s => new SuggestedWorkItem(s, workItems[s.WorkItemId]))
                            .ToList(),
                        Evidence = evidence.Where(e => evidenceLinks[requirement.Id].Contains(e.Id))
                            .Select(e => new EvidenceItem(e, e.IsExpiredOn(today)))
                            .ToList(),
                        EvidenceSuggestions = evidenceSuggestions[requirement.Id].ToList()
                    })
                    .ToList();

                result.Add(new FrameworkCompliance(framework, version, rows));
            }

            // US-020: the pending AI suggestions, best first per story
            var requirements = result.SelectMany(f => f.Requirements).ToDictionary(r => r.Requirement.Id, r => r.Requirement);
            var pending = pendingSuggestions
                .Where(s => requirements.ContainsKey(s.RequirementId))
                .OrderBy(s => s.Rank)
                .Select(s => new PendingSuggestion(s, requirements[s.RequirementId]))
                .ToList();

            return new ProjectCompliance(
                result.OrderBy(f => f.Framework.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                workItems.Values.ToList())
            {
                Suggestions = pending
            };
        }
    }

    public record ProjectCompliance(IReadOnlyList<FrameworkCompliance> Frameworks, IReadOnlyList<WorkItem> WorkItems)
    {
        // US-020: pending AI suggestions for the project's stories
        public IReadOnlyList<PendingSuggestion> Suggestions { get; init; } = [];

        public IEnumerable<PendingSuggestion> SuggestionsFor(Guid workItemId) => Suggestions.Where(s => s.Suggestion.WorkItemId == workItemId);

        // US-044 R9: the framework (sub-tab) a requirement belongs to
        public FrameworkCompliance? FrameworkOf(Requirement requirement)
            => Frameworks.FirstOrDefault(f => f.Version.Id == requirement.FrameworkVersionId);

        // US-021 R3: per user story, the codes of the requirements it is linked to
        public IReadOnlyList<string> RequirementCodesOf(Guid workItemId)
            => Frameworks.SelectMany(f => f.Requirements)
                .Where(r => r.Links.Any(l => l.WorkItem.Id == workItemId))
                .Select(r => r.Requirement.Code)
                .Distinct()
                .ToList();
    }

    // One framework version the project follows, with its requirements in the document's order
    public record FrameworkCompliance(Framework Framework, FrameworkVersion Version, IReadOnlyList<RequirementCoverage> Requirements)
    {
        // US-043 R6: requirements that are not applicable do not count
        public int Total => Requirements.Count(r => r.IsApplicable);
        public int Covered => Requirements.Count(r => r.IsApplicable && r.IsCovered);
        public int NotApplicable => Requirements.Count(r => !r.IsApplicable);

        // US-044 R1: the applicable requirements without a link, and the pending AI suggestions for applicable ones
        public int NotCovered => Total - Covered;
        public int SuggestionCount => Requirements.Where(r => r.IsApplicable).Sum(r => r.Suggestions.Count);

        // US-057 R4: applicable requirements with at least one evidence that has not expired
        public int WithEvidence => Requirements.Count(r => r.HasEvidence);

        // US-058: suggestions from tests and pull requests that wait for a person
        public int EvidenceSuggestionCount => Requirements.Sum(r => r.PendingEvidenceSuggestions.Count());

        // e.g. "12 of 20 requirements covered (60 %), 2 not applicable"
        public string CoverageText
            => $"{Covered} of {Total} requirements covered ({Percent} %)" + (NotApplicable > 0 ? $", {NotApplicable} not applicable" : "");

        // US-021 R4: rounded to whole per cent; 0 when there are no requirements
        public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Covered / Total, MidpointRounding.AwayFromZero);
    }

    // NotApplicableReason: set when the requirement does not apply to the project (US-043)
    public record RequirementCoverage(Requirement Requirement, IReadOnlyList<LinkedWorkItem> Links, string? NotApplicableReason = null)
    {
        public bool IsApplicable => NotApplicableReason is null;

        // US-021 R2, R5: covered when at least one active link exists
        public bool IsCovered => Links.Count > 0;

        // US-044 R4: the pending AI suggestions for this requirement, best first
        public IReadOnlyList<SuggestedWorkItem> Suggestions { get; init; } = [];

        public bool HasSuggestions => IsApplicable && Suggestions.Count > 0;

        // US-057: the evidence on this requirement, newest first. R3, R8: expired or not applicable does not count.
        public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];

        public bool HasEvidence => IsApplicable && Evidence.Any(e => !e.IsExpired);

        // US-058 R6: pending suggestions from tests and pull requests, and blocked ones (failing tests); none when not applicable
        public IReadOnlyList<Domain.Mappings.EvidenceSuggestion> EvidenceSuggestions { get; init; } = [];

        public IEnumerable<Domain.Mappings.EvidenceSuggestion> PendingEvidenceSuggestions
            => IsApplicable ? EvidenceSuggestions.Where(s => s.IsPending) : [];

        // R2: the failing tests of the newest blocked run, e.g. "1 failing test"
        public int FailingTests
            => IsApplicable ? EvidenceSuggestions.Where(s => s.Status == Domain.Mappings.EvidenceSuggestionStatus.Blocked).Select(s => s.FailingCount).FirstOrDefault() : 0;
    }

    public record EvidenceItem(Domain.Mappings.Evidence Evidence, bool IsExpired);

    public record SuggestedWorkItem(Domain.Mappings.MappingSuggestion Suggestion, WorkItem WorkItem);

    // The link itself is kept for the traceability matrix (US-052): source, when and by whom
    public record LinkedWorkItem(Domain.Mappings.RequirementLink Link, WorkItem WorkItem)
    {
        public Guid LinkId => Link.Id;
    }

    public record PendingSuggestion(Domain.Mappings.MappingSuggestion Suggestion, Requirement Requirement);
}
