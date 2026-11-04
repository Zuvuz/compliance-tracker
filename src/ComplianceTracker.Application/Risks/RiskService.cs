using ComplianceTracker.Domain.Users;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Risks
{
    // Storage for risks (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IRiskRepository
    {
        // The project's risks, also closed ones, with their measures
        Task<IReadOnlyList<Risk>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<Risk?> GetAsync(Guid projectId, Guid riskId, CancellationToken cancellationToken = default);

        // By id alone; the caller checks the project's organisation
        Task<Risk?> GetByIdAsync(Guid riskId, CancellationToken cancellationToken = default);

        Task AddAsync(Risk risk, CancellationToken cancellationToken = default);
    }

    // The fields of a risk as a form sends them (US-042 R1, R3)
    public record RiskInput(
        string Title, string? Description, int Likelihood, int Impact, Guid OwnerId, RiskTreatment Treatment,
        int? ResidualLikelihood = null, int? ResidualImpact = null);

    // US-042: the project's risk register. Every change is logged with the old and new values (R9).
    public class RiskService
    {
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly ComplianceQueries _compliance;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public RiskService(
            IProjectRepository projects,
            IRiskRepository risks,
            ComplianceQueries compliance,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _risks = risks;
            _compliance = compliance;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Risk> AddAsync(Guid projectId, RiskInput input, CancellationToken cancellationToken = default)
        {
            var (organization, userId, project) = await OpenProjectAsync(projectId, cancellationToken);
            EnsureMember(organization, input.OwnerId);

            var risk = Risk.Create(project.Id, input.Title, input.Description, input.Likelihood, input.Impact, input.OwnerId, input.Treatment, userId, Now);
            if (input.ResidualLikelihood is not null || input.ResidualImpact is not null)
            {
                risk.Update(input.Title, input.Description, input.Likelihood, input.Impact, input.OwnerId, input.Treatment,
                    input.ResidualLikelihood, input.ResidualImpact);
            }

            await _risks.AddAsync(risk, cancellationToken);
            await _log.LogAsync(ActivityAction.RiskAdded, project.Id,
                $"{risk.Title}: likelihood {risk.Likelihood}, impact {risk.Impact} ({risk.Level})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return risk;
        }

        // US-092: expectedVersion is the version the form was opened with
        public async Task UpdateAsync(Guid projectId, Guid riskId, RiskInput input, CancellationToken cancellationToken = default, uint? expectedVersion = null)
        {
            var (organization, _, project, risk) = await RiskAsync(projectId, riskId, cancellationToken);
            Concurrency.Ensure(risk.Version, expectedVersion);
            EnsureMember(organization, input.OwnerId);

            var changes = risk.Update(input.Title, input.Description, input.Likelihood, input.Impact, input.OwnerId, input.Treatment,
                input.ResidualLikelihood, input.ResidualImpact);
            if (changes.Count > 0)
            {
                await LogChangeAsync(organization, project, risk, changes, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // R4
        public async Task AcceptAsync(Guid projectId, Guid riskId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, userId, project, risk) = await RiskAsync(projectId, riskId, cancellationToken, Permission.DecideCompliance);
            await LogChangeAsync(organization, project, risk, [risk.Accept(reason, userId, Now, organization.Users.First(u => u.Id == userId).Role)], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R7
        public async Task CloseAsync(Guid projectId, Guid riskId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, _, project, risk) = await RiskAsync(projectId, riskId, cancellationToken, Permission.DecideCompliance);
            await LogChangeAsync(organization, project, risk, [risk.Close(reason, Now)], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6
        public async Task MarkReviewedAsync(Guid projectId, Guid riskId, CancellationToken cancellationToken = default)
        {
            var (_, userId, project, risk) = await RiskAsync(projectId, riskId, cancellationToken);
            risk.MarkReviewed(userId, Now);
            await _log.LogAsync(ActivityAction.RiskReviewed, project.Id, risk.Title, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: a requirement of a framework version the project follows, or one of the project's user stories
        public async Task AddMeasureAsync(Guid projectId, Guid riskId, Guid? requirementId, Guid? workItemId, CancellationToken cancellationToken = default)
        {
            var (organization, _, project, risk) = await RiskAsync(projectId, riskId, cancellationToken);
            var compliance = await _compliance.ForProjectAsync(project.Id, cancellationToken)
                ?? throw new DomainException("Project not found");

            string name;
            if (requirementId is { } rid)
            {
                name = compliance.Frameworks.SelectMany(f => f.Requirements).FirstOrDefault(r => r.Requirement.Id == rid)?.Requirement.Code
                    ?? throw new DomainException("The requirement is not in a framework version the project follows");
            }
            else
            {
                name = compliance.WorkItems.FirstOrDefault(w => w.Id == workItemId)?.Title
                    ?? throw new DomainException("User story not found");
            }

            risk.AddMeasure(requirementId, workItemId);
            await LogChangeAsync(organization, project, risk, [new ProjectChange("Measure", "", name)], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveMeasureAsync(Guid projectId, Guid riskId, Guid measureId, CancellationToken cancellationToken = default)
        {
            var (organization, _, project, risk) = await RiskAsync(projectId, riskId, cancellationToken);
            risk.RemoveMeasure(measureId);
            await LogChangeAsync(organization, project, risk, [new ProjectChange("Measure", "linked", "")], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R9: e.g. "Stolen session cookie: Likelihood: 3 → 2". Owners are shown by name, not by id.
        private async Task LogChangeAsync(Organization organization, Project project, Risk risk, IEnumerable<ProjectChange> changes, CancellationToken cancellationToken)
        {
            string Name(string value)
                => Guid.TryParse(value, out var id) ? organization.Users.FirstOrDefault(u => u.Id == id)?.Name ?? value : value;

            var text = string.Join("; ", changes.Select(c => c.Field == "Owner" ? c with { OldValue = Name(c.OldValue), NewValue = Name(c.NewValue) } : c));
            await _log.LogAsync(ActivityAction.RiskUpdated, project.Id, $"{risk.Title}: {text}", cancellationToken);
        }

        private static void EnsureMember(Organization organization, Guid userId)
        {
            if (organization.Users.All(u => u.Id != userId))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }
        }

        // The member's own project; US-024 R4 (R10): nothing changes at end of support
        private async Task<(Organization Organization, Guid UserId, Project Project)> OpenProjectAsync(
            Guid projectId, CancellationToken cancellationToken, Permission permission = Permission.ManageRisks)
        {
            var (organization, user) = await _member.RequireAsync(permission, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization, user.Id, project);
        }

        private async Task<(Organization Organization, Guid UserId, Project Project, Risk Risk)> RiskAsync(
            Guid projectId, Guid riskId, CancellationToken cancellationToken, Permission permission = Permission.ManageRisks)
        {
            var (organization, userId, project) = await OpenProjectAsync(projectId, cancellationToken, permission);
            var risk = await _risks.GetAsync(project.Id, riskId, cancellationToken)
                ?? throw new DomainException("Risk not found");
            return (organization, userId, project, risk);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // What the Risks tab reads (US-042 R8, R11)
    public class RiskQueries
    {
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly CurrentMember _member;

        public RiskQueries(IProjectRepository projects, IRiskRepository risks, CurrentMember member)
        {
            _projects = projects;
            _risks = risks;
            _member = member;
        }

        // The project's risks, highest level first (then score, then title); null for other organisations' projects
        public async Task<IReadOnlyList<Risk>?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is null)
            {
                return null;
            }

            return (await _risks.ListAsync(projectId, cancellationToken))
                .OrderByDescending(r => r.Level)
                .ThenByDescending(r => r.Score)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // One risk, if its project belongs to the user's organisation (R11)
        public async Task<Risk?> GetAsync(Guid riskId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _risks.GetByIdAsync(riskId, cancellationToken) is not { } risk
                || await _projects.GetAsync(organization.Id, risk.ProjectId, cancellationToken) is null)
            {
                return null;
            }

            return risk;
        }
    }
}
