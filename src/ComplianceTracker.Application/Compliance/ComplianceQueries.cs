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
        private readonly IApplicabilitySuggestionRepository _applicabilitySuggestions;
        private readonly Risks.IRiskRepository _risks;
        private readonly IClassificationRepository _classifications;

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
            TimeProvider clock,
            IApplicabilitySuggestionRepository applicabilitySuggestions,
            Risks.IRiskRepository risks,
            IClassificationRepository classifications)
        {
            _applicabilitySuggestions = applicabilitySuggestions;
            _risks = risks;
            _classifications = classifications;
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
            var decisions = (await _applicability.ListActiveAsync(project.Id, cancellationToken))
                .ToDictionary(a => a.RequirementId);

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
                .ToLookup(l => l.RequirementId);

            // US-058: suggestions from tests and pull requests that wait for a person, and the newest run's failing tests
            var evidenceSuggestions = (await _evidenceSuggestions.ListAsync(project.Id, cancellationToken))
                .Where(s => s.Status is Domain.Mappings.EvidenceSuggestionStatus.Pending or Domain.Mappings.EvidenceSuggestionStatus.Blocked)
                .OrderByDescending(s => s.CreatedAt)
                .ToLookup(s => s.RequirementId);

            // US-060 R4: requirements named by the measures of an open High or Critical risk
            var projectRisks = await _risks.ListAsync(project.Id, cancellationToken);
            var highRisk = projectRisks
                .Where(r => r.Status != Domain.Risks.RiskStatus.Closed && r.Level >= Domain.Risks.RiskLevel.High)
                .SelectMany(r => r.Measures).Select(m => m.RequirementId).OfType<Guid>().ToHashSet();

            // US-124 R2: the open risks that reach each requirement, directly or through a story
            var risksByRequirement = Risks.RiskTraces.ByRequirement(projectRisks,
                links.Where(l => workItems.ContainsKey(l.WorkItemId) && workItems[l.WorkItemId].State != WorkItemState.Removed)
                    .ToLookup(l => l.WorkItemId, l => l.RequirementId));

            // US-060 R6: pending "not applicable" suggestions. US-061 R3: decisions accepted from a suggestion that relied
            // on an earlier classification than the current one, and not decided again since.
            var applicabilitySuggestions = await _applicabilitySuggestions.ListAsync(project.Id, cancellationToken);
            var pendingApplicability = applicabilitySuggestions.Where(s => s.IsPending).ToLookup(s => s.RequirementId);
            var currentClassification = (await _classifications.ListAsync(project.Id, cancellationToken))
                .GroupBy(c => c.FrameworkId).ToDictionary(g => g.Key, g => g.Max(c => c.ClassifiedAt));
            var activeDecisions = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).ToDictionary(a => a.RequirementId);
            var fromOldClassification = applicabilitySuggestions
                .Where(s => s.Status == Domain.Mappings.SuggestionStatus.Approved
                    && currentClassification.TryGetValue(s.FrameworkId, out var classifiedAt)
                    && s.ClassifiedAt != classifiedAt
                    && activeDecisions.TryGetValue(s.RequirementId, out var decision)
                    && decision.DecidedAt < classifiedAt)
                .Select(s => s.RequirementId)
                .ToHashSet();

            var result = new List<FrameworkCompliance>();
            foreach (var used in project.Frameworks)
            {
                var framework = frameworks.FirstOrDefault(f => f.Id == used.FrameworkId);
                var version = framework?.Versions.FirstOrDefault(v => v.Id == used.FrameworkVersionId);
                if (framework is null || version is null)
                {
                    continue;
                }

                // US-110: every version's requirements, to show the old text of what waits for review
                var allRequirements = framework.Versions.SelectMany(v => v.Requirements).ToDictionary(r => r.Id);

                var rows = version.Requirements
                    .Select(requirement => new RequirementCoverage(
                        requirement,
                        links.Where(l => l.RequirementId == requirement.Id && workItems.ContainsKey(l.WorkItemId))
                            .Select(l => new LinkedWorkItem(l, workItems[l.WorkItemId]))
                            .OrderBy(l => l.WorkItem.Title, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        decisions.GetValueOrDefault(requirement.Id)?.Reason)
                    {
                        NotApplicableNeedsReview = decisions.GetValueOrDefault(requirement.Id)?.NeedsReview ?? false,
                        PreviousRequirement = PreviousOf(requirement.Id, links, evidenceLinks, decisions, allRequirements),
                        // US-044 R4: best first
                        Suggestions = pendingSuggestions.Where(s => s.RequirementId == requirement.Id)
                            .OrderByDescending(s => s.Score)
                            .Select(s => new SuggestedWorkItem(s, workItems[s.WorkItemId]))
                            .ToList(),
                        Evidence = evidence.Where(e => evidenceLinks[requirement.Id].Any(l => l.EvidenceId == e.Id))
                            .Select(e => new EvidenceItem(e, e.IsExpiredOn(today))
                            {
                                NeedsReview = evidenceLinks[requirement.Id].Any(l => l.EvidenceId == e.Id && l.NeedsReview)
                            })
                            .ToList(),
                        EvidenceSuggestions = evidenceSuggestions[requirement.Id].ToList(),
                        HighRisk = highRisk.Contains(requirement.Id),
                        Risks = risksByRequirement.GetValueOrDefault(requirement.Id) ?? [],
                        ApplicabilitySuggestion = pendingApplicability[requirement.Id].OrderByDescending(s => s.CreatedAt).FirstOrDefault(),
                        NotApplicableFromOldClassification = fromOldClassification.Contains(requirement.Id)
                    })
                    .ToList();

                result.Add(new FrameworkCompliance(framework, version, rows));
            }

            // US-020: the pending AI suggestions, best first per story
            var requirements = result.SelectMany(f => f.Requirements).ToDictionary(r => r.Requirement.Id, r => r.Requirement);
            var pending = pendingSuggestions
                .Where(s => requirements.ContainsKey(s.RequirementId))
                .OrderByDescending(s => highRisk.Contains(s.RequirementId)) // US-060 R4
                .ThenBy(s => s.Rank)
                .Select(s => new PendingSuggestion(s, requirements[s.RequirementId]))
                .ToList();

            return new ProjectCompliance(
                result.OrderBy(f => f.Framework.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                workItems.Values.ToList())
            {
                Suggestions = pending
            };
        }

        // US-110 R4: the requirement in the earlier version that something waiting for review was carried from
        private static Requirement? PreviousOf(
            Guid requirementId,
            IReadOnlyList<Domain.Mappings.RequirementLink> links,
            ILookup<Guid, Domain.Mappings.EvidenceLink> evidenceLinks,
            Dictionary<Guid, Domain.Mappings.RequirementApplicability> decisions,
            Dictionary<Guid, Requirement> allRequirements)
        {
            var from = links.Where(l => l.RequirementId == requirementId && l.NeedsReview).Select(l => l.CarriedFromRequirementId)
                .Concat(evidenceLinks[requirementId].Where(l => l.NeedsReview).Select(l => l.CarriedFromRequirementId))
                .Append(decisions.GetValueOrDefault(requirementId) is { NeedsReview: true } d ? d.CarriedFromRequirementId : null)
                .FirstOrDefault(id => id is not null);

            return from is { } id ? allRequirements.GetValueOrDefault(id) : null;
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

        // US-110: requirements with something carried over that waits for review
        public int NeedsReview => Requirements.Count(r => r.NeedsReview);

        // US-044 R1: the applicable requirements without a link, and the pending AI suggestions for applicable ones
        public int NotCovered => Total - Covered;
        public int SuggestionCount => Requirements.Where(r => r.IsApplicable).Sum(r => r.Suggestions.Count);

        // US-060 R6: pending "not applicable" suggestions. US-061 R3: decisions to look at after a new classification.
        public int ApplicabilitySuggestionCount => Requirements.Count(r => r.ApplicabilitySuggestion is not null);
        public int OldClassificationDecisions => Requirements.Count(r => r.NotApplicableFromOldClassification);

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

        // US-021 R2, R5: covered when at least one active link exists. US-110 R3: a link waiting for review does not count.
        public bool IsCovered => Links.Any(l => !l.Link.NeedsReview);

        // US-110 R3: linked, but only by links that wait for review: "Covered, needs review"
        public bool IsCoveredNeedingReview => !IsCovered && Links.Count > 0;

        // US-110: the "not applicable" decision waits for review
        public bool NotApplicableNeedsReview { get; init; }

        // US-110 R4: the requirement in the earlier version, for the old text
        public Requirement? PreviousRequirement { get; init; }

        // US-110: something on this requirement was carried over and waits for a person
        public bool NeedsReview => Links.Any(l => l.Link.NeedsReview) || Evidence.Any(e => e.NeedsReview) || NotApplicableNeedsReview;

        // US-060 R4: named by a measure of an open High or Critical risk
        public bool HighRisk { get; init; }

        // US-124 R2: the open risks that reach this requirement, highest first
        public IReadOnlyList<Risks.RiskReference> Risks { get; init; } = [];

        // US-060 R6: the AI suggests that the requirement does not apply; a person decides
        public Domain.Mappings.ApplicabilitySuggestion? ApplicabilitySuggestion { get; init; }

        // US-061 R3: not applicable from a suggestion that relied on an earlier classification
        public bool NotApplicableFromOldClassification { get; init; }

        // US-044 R4: the pending AI suggestions for this requirement, best first
        public IReadOnlyList<SuggestedWorkItem> Suggestions { get; init; } = [];

        public bool HasSuggestions => IsApplicable && Suggestions.Count > 0;

        // US-057: the evidence on this requirement, newest first. R3, R8: expired or not applicable does not count.
        public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];

        public bool HasEvidence => IsApplicable && Evidence.Any(e => !e.IsExpired && !e.NeedsReview);

        // US-058 R6: pending suggestions from tests and pull requests, and blocked ones (failing tests); none when not applicable
        public IReadOnlyList<Domain.Mappings.EvidenceSuggestion> EvidenceSuggestions { get; init; } = [];

        public IEnumerable<Domain.Mappings.EvidenceSuggestion> PendingEvidenceSuggestions
            => IsApplicable ? EvidenceSuggestions.Where(s => s.IsPending) : [];

        // R2: the failing tests of the newest blocked run, e.g. "1 failing test"
        public int FailingTests
            => IsApplicable ? EvidenceSuggestions.Where(s => s.Status == Domain.Mappings.EvidenceSuggestionStatus.Blocked).Select(s => s.FailingCount).FirstOrDefault() : 0;
    }

    public record EvidenceItem(Domain.Mappings.Evidence Evidence, bool IsExpired)
    {
        // US-110: the link to this requirement waits for review after a version change
        public bool NeedsReview { get; init; }
    }

    public record SuggestedWorkItem(Domain.Mappings.MappingSuggestion Suggestion, WorkItem WorkItem);

    // The link itself is kept for the traceability matrix (US-052): source, when and by whom
    public record LinkedWorkItem(Domain.Mappings.RequirementLink Link, WorkItem WorkItem)
    {
        public Guid LinkId => Link.Id;
    }

    public record PendingSuggestion(Domain.Mappings.MappingSuggestion Suggestion, Requirement Requirement);
}
