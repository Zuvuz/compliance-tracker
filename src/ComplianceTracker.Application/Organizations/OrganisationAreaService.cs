using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-142 R3: storage for the NIS2 scope checks (implemented in Infrastructure). Rows are only added.
    public interface INis2AssessmentRepository
    {
        // Every check of the organisation, newest first
        Task<IReadOnlyList<Nis2Assessment>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(Nis2Assessment assessment, CancellationToken cancellationToken = default);
    }

    // US-142: the fixed ids of the built-in NIS2 catalogues, given by Infrastructure (where the seed data lives)
    public record Nis2Catalogues(Guid Directive, Guid DanishLaw, Guid TechnicalMeasures);

    // What was answered in the scope check (R3)
    public record Nis2Answers(Sector Sector, Nis2Service Service, int Employees, decimal TurnoverMillions, decimal BalanceSheetMillions,
        bool DesignatedByAuthority);

    // The organisation area page: the area, the newest check and the earlier ones
    public record OrganisationAreaView(Project Area, Organization Organization, Nis2Assessment? Latest, IReadOnlyList<Nis2Assessment> History)
    {
        // R4: NIS2 is followed although the newest check says the organisation is not covered (or there is no check)
        public bool FollowsNis2ByChoice(Guid nis2Id) => Area.FrameworkInUse(nis2Id) is not null && Latest is not { IsCovered: true };
    }

    // US-142: the organisation area, where the whole company follows NIS2 and other organisation-level frameworks.
    // It is a project of the kind Organisation, so coverage, evidence, risks and reports work as for a product (R9).
    public class OrganisationAreaService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly INis2AssessmentRepository _assessments;
        private readonly OrganisationDocumentService _documents;
        private readonly Nis2Catalogues _catalogues;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public OrganisationAreaService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            INis2AssessmentRepository assessments,
            OrganisationDocumentService documents,
            Nis2Catalogues catalogues,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _assessments = assessments;
            _documents = documents;
            _catalogues = catalogues;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // R1: the organisation area, made the first time a member opens it
        public async Task<Project> EnsureAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            return await AreaAsync(organization.Id, cancellationToken);
        }

        public async Task<OrganisationAreaView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var area = await AreaAsync(organization.Id, cancellationToken);
            var history = await _assessments.ListAsync(organization.Id, cancellationToken);
            return new OrganisationAreaView(area, organization, history.FirstOrDefault(), history);
        }

        // R3, R6, R7: does the scope check, keeps it, and switches on what it finds: NIS2, the national law of the
        // organisation's country, and 2024/2690 for the digital providers it covers. Nothing is switched off: a framework
        // that no longer applies is removed by a person on the Frameworks tab. A compliance decision (DecideCompliance).
        public async Task<Nis2Assessment> AssessNis2Async(Nis2Answers answers, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var assessment = Nis2Assessment.Assess(organization.Id, answers.Sector, answers.Service, answers.Employees,
                answers.TurnoverMillions, answers.BalanceSheetMillions, answers.DesignatedByAuthority, user.Id, Now);
            await _assessments.AddAsync(assessment, cancellationToken);
            await _log.LogAsync(ActivityAction.Nis2ScopeChecked, organization.Id, $"{Text(assessment.Result)}: {assessment.Reason}", cancellationToken);

            if (assessment.IsCovered)
            {
                var area = await AreaAsync(organization.Id, cancellationToken);
                await FollowAsync(organization.Id, area, _catalogues.Directive, user.Id, cancellationToken);
                if (organization.Country == "DK")
                {
                    await FollowAsync(organization.Id, area, _catalogues.DanishLaw, user.Id, cancellationToken);
                }

                if (assessment.Uses2024_2690)
                {
                    await FollowAsync(organization.Id, area, _catalogues.TechnicalMeasures, user.Id, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _documents.SyncAsync(organization.Id, cancellationToken); // US-136 R1
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return assessment;
        }

        // R6: an Admin sets the EU country the organisation is established in
        public async Task SetCountryAsync(string country, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            if (organization.SetCountry(country, user.Id))
            {
                await _log.LogAsync(ActivityAction.OrganisationCountryChanged, organization.Id, organization.Country, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        public static string Text(Nis2Result result) => result switch
        {
            Nis2Result.Essential => "Essential",
            Nis2Result.Important => "Important",
            _ => "Not covered"
        };

        private async Task<Project> AreaAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            if (await _projects.GetOrganisationAreaAsync(organizationId, cancellationToken) is { } area)
            {
                return area;
            }

            area = Project.CreateOrganisationArea(organizationId);
            await _projects.AddAsync(area, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return area;
        }

        // Switches the built-in framework on for the organisation (US-006) and lets the area follow its current version
        private async Task FollowAsync(Guid organizationId, Project area, Guid frameworkId, Guid userId, CancellationToken cancellationToken)
        {
            var framework = await _frameworks.GetBuiltInAsync(frameworkId, cancellationToken)
                ?? throw new DomainException("The built-in framework is missing");
            var version = framework.RequireCurrentVersion(DateOnly.FromDateTime(Now));
            if (await _frameworks.GetActiveSelectionAsync(organizationId, version.Id, cancellationToken) is null)
            {
                await _frameworks.AddSelectionAsync(OrganizationFramework.Enable(organizationId, version.Id, userId, Now), cancellationToken);
                await _log.LogAsync(ActivityAction.FrameworkEnabled, version.Id, cancellationToken);
            }

            if (area.FrameworkInUse(framework.Id) is null)
            {
                area.UseFramework(framework, version, userId, Now);
                await _log.LogAsync(ActivityAction.ProjectFrameworkAdded, area.Id, framework.Name, cancellationToken);
            }
        }
    }
}
