using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Compliance
{
    // US-115 R4: one requirement of the organisation on a product's page: its status in the organisation, whether the
    // product opted out (and why), and, where it is carried out in the projects, this product's own proof
    public sealed record InheritedRow(FrameworkCompliance Framework, RequirementCoverage Organisation, string? OptOutReason,
        IReadOnlyList<Evidence> ProofHere)
    {
        public bool IsOptedOut => OptOutReason is not null;
        public bool IsCarriedOut => Organisation.Scope == RequirementScope.CarriedOutInProjects;
    }

    // The product's "From the organisation" page; Area is null before the organisation area is made
    public sealed record InheritedView(Project Project, Project? Area, IReadOnlyList<InheritedRow> Rows, IReadOnlyList<Evidence> Pickable)
    {
        public IEnumerable<IGrouping<FrameworkCompliance, InheritedRow>> ByFramework => Rows.GroupBy(r => r.Framework);
    }

    // US-115: the organisation's requirements on a product ("down"), a product's opt-out (R4), a product's proof of a
    // requirement carried out in the projects ("up"), and where a requirement is met (R2)
    public class InheritedRequirements
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly ComplianceQueries _compliance;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IEvidenceRepository _evidence;
        private readonly IRequirementScopeDecisionRepository _scopes;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public InheritedRequirements(IProjectRepository projects, IFrameworkRepository frameworks, ComplianceQueries compliance,
            IRequirementApplicabilityRepository applicability, IEvidenceRepository evidence, IRequirementScopeDecisionRepository scopes,
            CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _compliance = compliance;
            _applicability = applicability;
            _evidence = evidence;
            _scopes = scopes;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // R4: the requirements the organisation follows, as a product sees them; null for another organisation's project or
        // for the organisation area itself
        public async Task<InheritedView?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { IsOrganisationArea: false } project)
            {
                return null;
            }

            var area = await _projects.GetOrganisationAreaAsync(organization.Id, cancellationToken);
            if (area is null || await _compliance.ForProjectAsync(area.Id, cancellationToken) is not { } organisation)
            {
                return new InheritedView(project, area, [], []);
            }

            var optOuts = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).ToDictionary(a => a.RequirementId);
            var evidence = await _evidence.ListActiveAsync(project.Id, cancellationToken);
            var links = await _evidence.ListActiveLinksAsync(project.Id, cancellationToken);
            var today = DateOnly.FromDateTime(Now);
            var rows = organisation.Frameworks
                .SelectMany(f => f.Requirements.Where(r => r.IsApplicable).Select(r => new InheritedRow(f, r,
                    optOuts.GetValueOrDefault(r.Requirement.Id)?.Reason,
                    evidence.Where(e => links.Any(l => l.EvidenceId == e.Id && l.RequirementId == r.Requirement.Id))
                        .OrderByDescending(e => e.AddedAt).ToList())))
                .ToList();
            var pickable = evidence.Where(e => !e.IsExpiredOn(today)).OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToList();
            return new InheritedView(project, area, rows, pickable);
        }

        // R4: a product outside the ISMS's scope opts out of an inherited requirement, with a reason (DecideCompliance)
        public async Task OptOutAsync(Guid projectId, Guid requirementId, string reason, CancellationToken cancellationToken = default)
        {
            var (organization, user, project) = await ProductAsync(projectId, Permission.DecideCompliance, cancellationToken);
            var (_, requirement) = await InheritedAsync(organization, requirementId, cancellationToken);
            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirement.Id))
            {
                throw new DomainException($"{requirement.Code} already does not apply to {project.Name}");
            }

            var decision = RequirementApplicability.NotApplicable(project.Id, requirement.Id, reason, user.Id, Now, user.Role);
            await _applicability.AddAsync(decision, cancellationToken);
            await _log.LogAsync(ActivityAction.RequirementMarkedNotApplicable, project.Id,
                $"{requirement.Code} (from the organisation): {decision.Reason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R4: the inherited requirement applies to the product again
        public async Task OptInAsync(Guid projectId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organization, user, project) = await ProductAsync(projectId, Permission.DecideCompliance, cancellationToken);
            var (_, requirement) = await InheritedAsync(organization, requirementId, cancellationToken);
            var decision = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).FirstOrDefault(a => a.RequirementId == requirement.Id)
                ?? throw new DomainException($"{requirement.Code} already applies to {project.Name}");
            decision.Revoke(user.Id, Now);
            await _log.LogAsync(ActivityAction.RequirementMarkedApplicable, project.Id, $"{requirement.Code} (from the organisation)", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // "Up": the product's own evidence proves a requirement the organisation carries out in the projects (LinkWork)
        public async Task LinkProofAsync(Guid projectId, Guid requirementId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            var (organization, user, project) = await ProductAsync(projectId, Permission.LinkWork, cancellationToken);
            var (framework, requirement) = await InheritedAsync(organization, requirementId, cancellationToken);
            if (await ScopeOfAsync(organization.Id, framework, requirement, cancellationToken) != RequirementScope.CarriedOutInProjects)
            {
                throw new DomainException($"{requirement.Code} is met by the organisation, not in the projects");
            }

            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirement.Id))
            {
                throw new DomainException($"{requirement.Code} does not apply to {project.Name}");
            }

            var evidence = await _evidence.GetAsync(project.Id, evidenceId, cancellationToken) ?? throw new DomainException("Evidence not found");
            if ((await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).Any(l => l.EvidenceId == evidence.Id && l.RequirementId == requirement.Id))
            {
                throw new DomainException($"{evidence.Title} is already linked to {requirement.Code}");
            }

            await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, user.Role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceLinked, project.Id, $"{requirement.Code} (for the organisation): {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2, R7: where a requirement of the organisation area is met, with a reason (DecideCompliance)
        public async Task ChangeScopeAsync(Guid requirementId, RequirementScope scope, string reason, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var area = await _projects.GetOrganisationAreaAsync(organization.Id, cancellationToken) ?? throw new DomainException("The organisation follows no framework yet");
            var (framework, requirement) = await InheritedAsync(organization, requirementId, cancellationToken);
            if (await ScopeOfAsync(organization.Id, framework, requirement, cancellationToken) == scope)
            {
                throw new DomainException($"{requirement.Code} is already {RequirementScopeDecision.NameOf(scope).ToLowerInvariant()}");
            }

            var decision = RequirementScopeDecision.Decide(organization.Id, requirement.Id, scope, reason, user.Id, user.Role, Now);
            await _scopes.AddAsync(decision, cancellationToken);
            await _log.LogAsync(ActivityAction.RequirementScopeChanged, area.Id,
                $"{requirement.Code}: {RequirementScopeDecision.NameOf(scope)} ({decision.Reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<RequirementScope> ScopeOfAsync(Guid organizationId, Framework framework, Requirement requirement, CancellationToken cancellationToken)
        {
            var decision = (await _scopes.ListNewestAsync(organizationId, cancellationToken)).FirstOrDefault(d => d.RequirementId == requirement.Id);
            return OrganisationScopes.Resolve(framework, requirement, decision).Scope;
        }

        private async Task<(Domain.Organizations.Organization Organization, User User, Project Project)> ProductAsync(
            Guid projectId, Permission permission, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(permission, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsOrganisationArea)
            {
                throw new DomainException("The organisation's own requirements are changed on its own page");
            }

            if (project.IsClosed)
            {
                throw new DomainException($"{project.Name} is closed");
            }

            return (organization, user, project);
        }

        // A requirement in a framework version the organisation area follows
        private async Task<(Framework Framework, Requirement Requirement)> InheritedAsync(Domain.Organizations.Organization organization, Guid requirementId, CancellationToken cancellationToken)
        {
            var area = await _projects.GetOrganisationAreaAsync(organization.Id, cancellationToken)
                ?? throw new DomainException("The organisation follows no framework yet");
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(organization.Id, area.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var versions = area.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();
            foreach (var framework in frameworks)
            {
                if (framework.Versions.Where(v => versions.Contains(v.Id)).SelectMany(v => v.Requirements).FirstOrDefault(r => r.Id == requirementId) is { } requirement)
                {
                    return (framework, requirement);
                }
            }

            throw new DomainException("The requirement is not one the organisation follows");
        }
    }
}
