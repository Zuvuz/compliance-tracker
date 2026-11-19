using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-115 R2: storage for "where a requirement is met" decisions (implemented in Infrastructure). Rows are only added.
    public interface IRequirementScopeDecisionRepository
    {
        // The newest decision per requirement
        Task<IReadOnlyList<RequirementScopeDecision>> ListNewestAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(RequirementScopeDecision decision, CancellationToken cancellationToken = default);
    }

    // US-115: where each requirement of the organisation area is met, and for those carried out in the projects, which
    // products have proven it (the "up" direction). Used by the organisation's compliance and by a product's inherited page.
    public class OrganisationScopes
    {
        private readonly IRequirementScopeDecisionRepository _decisions;
        private readonly IProjectRepository _projects;
        private readonly IEvidenceRepository _evidence;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly TimeProvider _clock;

        public OrganisationScopes(IRequirementScopeDecisionRepository decisions, IProjectRepository projects, IEvidenceRepository evidence,
            IRequirementApplicabilityRepository applicability, TimeProvider clock)
        {
            _decisions = decisions;
            _projects = projects;
            _evidence = evidence;
            _applicability = applicability;
            _clock = clock;
        }

        // The organisation area's frameworks with each row's scope, and the products' proof where it is carried out there
        public async Task<List<FrameworkCompliance>> ApplyAsync(Guid organizationId, IReadOnlyList<FrameworkCompliance> frameworks, CancellationToken cancellationToken)
        {
            var decisions = (await _decisions.ListNewestAsync(organizationId, cancellationToken)).ToDictionary(d => d.RequirementId);
            var scoped = Scope(frameworks, decisions);
            var carried = scoped.SelectMany(f => f.Requirements).Where(r => r.Scope == RequirementScope.CarriedOutInProjects).Select(r => r.Requirement.Id).ToList();
            if (carried.Count == 0)
            {
                return scoped;
            }

            var products = (await _projects.ListAsync(organizationId, cancellationToken)).Where(p => !p.IsOrganisationArea && !p.IsArchived)
                .Select(p => (p.Id, p.Name)).ToList();
            var ids = products.Select(p => p.Id).ToList();
            var links = ids.Count == 0 ? [] : await _evidence.ListLinksToAsync(ids, carried, cancellationToken);
            var optOuts = ids.Count == 0 ? [] : await _applicability.ListActiveForProjectsAsync(ids, cancellationToken);
            return CarriedOut(scoped, products, links, optOuts, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));
        }

        // R2: the scope of every row; the newest decision wins over the default
        public static List<FrameworkCompliance> Scope(IReadOnlyList<FrameworkCompliance> frameworks, IReadOnlyDictionary<Guid, RequirementScopeDecision> decisions)
            => frameworks.Select(f => f with
            {
                Requirements = f.Requirements.Select(r =>
                {
                    var (scope, reason) = Resolve(f.Framework, r.Requirement, decisions.GetValueOrDefault(r.Requirement.Id));
                    return r with { Scope = scope, ScopeReason = reason };
                }).ToList()
            }).ToList();

        // R2: ISO/IEC 27001's development and change controls (A.8.25–A.8.32) are carried out in the projects by default;
        // everything else the organisation follows is organisation-wide
        public static (RequirementScope Scope, string? Reason) Resolve(Framework framework, Requirement requirement, RequirementScopeDecision? decision)
            => decision is not null ? (decision.Scope, decision.Reason)
                : FrameworkProfiles.CarriedOutInProjectsByDefault(framework.Name, requirement.Code)
                    ? (RequirementScope.CarriedOutInProjects, FrameworkProfiles.CarriedOutReason)
                    : (RequirementScope.OrganisationWide, null);

        // "Up": each product that has not opted out (R4) either has proof linked to the requirement, or not yet. Proof that has
        // expired, or documentation alone (US-204), does not count.
        public static List<FrameworkCompliance> CarriedOut(IReadOnlyList<FrameworkCompliance> frameworks, IReadOnlyList<(Guid Id, string Name)> products,
            IReadOnlyList<(EvidenceLink Link, Evidence Evidence)> links, IReadOnlyList<RequirementApplicability> optOuts, DateOnly today)
        {
            var proven = links.Where(l => l.Evidence.IsActive && !l.Evidence.IsExpiredOn(today) && !l.Link.NeedsReview && l.Evidence.Kind == EvidenceKind.Proof)
                .Select(l => (l.Link.ProjectId, l.Link.RequirementId)).ToHashSet();
            var out_ = optOuts.Where(a => a.IsActive).Select(a => (a.ProjectId, a.RequirementId)).ToHashSet();
            return frameworks.Select(f => f with
            {
                Requirements = f.Requirements.Select(r =>
                {
                    if (r.Scope != RequirementScope.CarriedOutInProjects)
                    {
                        return r;
                    }

                    var following = products.Where(p => !out_.Contains((p.Id, r.Requirement.Id))).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    return r with
                    {
                        CarriedOut = new CarriedOutStatus(
                            following.Where(p => proven.Contains((p.Id, r.Requirement.Id))).Select(p => p.Name).ToList(),
                            following.Where(p => !proven.Contains((p.Id, r.Requirement.Id))).Select(p => p.Name).ToList())
                    };
                }).ToList()
            }).ToList();
        }
    }
}
