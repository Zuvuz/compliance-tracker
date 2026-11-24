using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-208: storage for the ISMS scope and the interested parties (implemented in Infrastructure). Add only registers;
    // IUnitOfWork saves.
    public interface IIsmsScopeRepository
    {
        Task<IsmsScope?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(IsmsScope scope, CancellationToken cancellationToken = default);

        // The organisation's interested parties, removed ones too
        Task<IReadOnlyList<InterestedParty>> ListPartiesAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddPartyAsync(InterestedParty party, CancellationToken cancellationToken = default);
    }

    // The scope as typed on the page (R1)
    public sealed record ScopeInput(string? Text, string? Locations, string? Departments, string? InternalIssues, string? ExternalIssues,
        Guid? OwnerId, DateOnly? NextReviewOn);

    // One project on the page and its place in the scope (R2, R3)
    public sealed record ScopeProjectLine(Guid ProjectId, string Name, bool IsClosed, ScopeState State, string? Reason);

    // The page: the scope (null until someone writes it), the projects, the interested parties and the people who can own it
    public sealed record IsmsScopeView(
        IsmsScope? Scope,
        IReadOnlyList<ScopeProjectLine> Projects,
        IReadOnlyList<InterestedParty> Parties,
        IReadOnlyDictionary<Guid, string> Members,
        bool CanChange,
        DateOnly Today)
    {
        public string? OwnerName => Scope?.OwnerId is { } id ? Members.GetValueOrDefault(id) : null;

        public string? ApprovedByName => Scope?.ApprovedById is { } id ? Members.GetValueOrDefault(id) : null;

        // R5
        public bool ReviewOverdue => Scope?.IsReviewOverdue(Today) == true;
    }

    public class IsmsScopeService
    {
        private readonly IIsmsScopeRepository _scopes;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly IPdfWriter _pdf;
        private readonly ICurrentUser _currentUser;
        private readonly OrganisationAreaService _area;
        private readonly ReportService _reports;

        public IsmsScopeService(IIsmsScopeRepository scopes, IProjectRepository projects, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock, IPdfWriter pdf, ICurrentUser currentUser, OrganisationAreaService area, ReportService reports)
        {
            _pdf = pdf;
            _currentUser = currentUser;
            _area = area;
            _reports = reports;
            _scopes = scopes;
            _projects = projects;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // R6: everyone in the organisation may read it
        public async Task<IsmsScopeView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var scope = await _scopes.GetAsync(organization.Id, cancellationToken);
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken))
                .Where(p => !p.IsOrganisationArea)
                .Select(p => new ScopeProjectLine(p.Id, p.Name, p.IsClosed, scope?.StateOf(p.Id) ?? ScopeState.NotDecided,
                    scope?.Projects.FirstOrDefault(s => s.ProjectId == p.Id)?.Reason))
                .OrderBy(p => p.State)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var parties = (await _scopes.ListPartiesAsync(organization.Id, cancellationToken))
                .Where(p => p.IsActive)
                .OrderBy(p => p.Kind)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new IsmsScopeView(scope, projects, parties, organization.Users.ToDictionary(u => u.Id, u => u.Name),
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken), DateOnly.FromDateTime(Now));
        }

        // R1, R6: the log names what changed, not the texts
        public async Task DescribeAsync(ScopeInput input, CancellationToken cancellationToken = default)
        {
            var (organization, scope) = await ScopeToChangeAsync(cancellationToken);
            if (input.OwnerId is { } owner && organization.Users.All(u => u.Id != owner))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }

            var changed = scope.Describe(input.Text, input.Locations, input.Departments, input.InternalIssues, input.ExternalIssues,
                input.OwnerId, input.NextReviewOn, Now);
            if (changed.Count == 0)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.IsmsScopeChanged, organization.Id, string.Join(", ", changed), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: inside the scope
        public async Task IncludeAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, scope) = await ScopeToChangeAsync(cancellationToken);
            var project = await ProjectAsync(organization.Id, projectId, cancellationToken);
            if (scope.Include(project.Id, Now))
            {
                await _log.LogAsync(ActivityAction.IsmsScopeChanged, organization.Id, $"{project.Name}: inside the scope", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // R2: outside the scope, with the reason
        public async Task ExcludeAsync(Guid projectId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, scope) = await ScopeToChangeAsync(cancellationToken);
            var project = await ProjectAsync(organization.Id, projectId, cancellationToken);
            if (scope.Exclude(project.Id, reason, Now))
            {
                await _log.LogAsync(ActivityAction.IsmsScopeChanged, organization.Id,
                    $"{project.Name}: outside the scope, because {scope.Projects.First(p => p.ProjectId == project.Id).Reason}", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // 4.4: a new version of the scope
        public async Task<int> ApproveAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scope = await _scopes.GetAsync(organization.Id, cancellationToken) ?? throw new DomainException("Describe the scope before it is approved");
            var version = scope.Approve(user.Id, Now);
            await _log.LogAsync(ActivityAction.IsmsScopeApproved, organization.Id, $"Version {version}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return version;
        }

        // R4: the same interested party cannot be added twice
        public async Task<InterestedParty> AddPartyAsync(string? name, InterestedPartyKind? kind, IReadOnlyList<(string? Text, bool Handled)> requirements,
            CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var party = InterestedParty.Create(organization.Id, name, kind, requirements, Now);
            await EnsureUniqueAsync(organization.Id, party, cancellationToken);
            await _scopes.AddPartyAsync(party, cancellationToken);
            await _log.LogAsync(ActivityAction.InterestedPartyChanged, organization.Id, $"Added {party.Name} ({party.Kind}): {Count(party)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return party;
        }

        public async Task ChangePartyAsync(Guid partyId, string? name, InterestedPartyKind? kind, IReadOnlyList<(string? Text, bool Handled)> requirements,
            CancellationToken cancellationToken = default)
        {
            var (organization, party) = await PartyAsync(partyId, cancellationToken);
            party.Change(name, kind, requirements);
            await EnsureUniqueAsync(organization.Id, party, cancellationToken);
            await _log.LogAsync(ActivityAction.InterestedPartyChanged, organization.Id, $"Changed {party.Name} ({party.Kind}): {Count(party)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemovePartyAsync(Guid partyId, CancellationToken cancellationToken = default)
        {
            var (organization, party) = await PartyAsync(partyId, cancellationToken);
            party.Remove(Now);
            await _log.LogAsync(ActivityAction.InterestedPartyChanged, organization.Id, $"Removed {party.Name}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R1: the scope as a PDF, made from the same report model as the other reports (US-131)
        public async Task<ReportFile> PdfAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var view = await ViewAsync(cancellationToken);
            var now = Now;
            var document = IsmsScopeReport.Build(view, organization.Name, _currentUser.Identity.Name, now);
            var header = new PdfHeader(organization.Name, "Organisation", IsmsScopeReport.Title, now, _currentUser.Identity.Name);
            return new ReportFile(IsmsScopeReport.Kind, document.Title,
                $"isms-scope-{Frameworks.FrameworkSlugs.Slug(organization.Name)}-{DateOnly.FromDateTime(now):yyyy-MM-dd}.pdf", "application/pdf",
                _pdf.FromReport(document, header));
        }

        // R1: an approved version can be locked with a fingerprint (US-050) on the organisation area's Reports tab
        public async Task<Domain.Reports.ReportSnapshot> LockAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scope = await _scopes.GetAsync(organization.Id, cancellationToken);
            if (scope is null || scope.ChangedSinceApproval)
            {
                throw new DomainException("Approve the scope before it is locked");
            }

            var area = await _area.EnsureAsync(cancellationToken);
            return await _reports.LockFileAsync(area.Id, await PdfAsync(cancellationToken), cancellationToken);
        }

        // e.g. "2 of 3 requirements handled"
        public static string Count(InterestedParty party) => $"{party.Handled} of {party.Requirements.Count} requirements handled";

        private async Task<(Domain.Organizations.Organization Organization, IsmsScope Scope)> ScopeToChangeAsync(CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scope = await _scopes.GetAsync(organization.Id, cancellationToken);
            if (scope is null)
            {
                scope = IsmsScope.Create(organization.Id, Now);
                await _scopes.AddAsync(scope, cancellationToken);
            }

            return (organization, scope);
        }

        private async Task<Domain.Projects.Project> ProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
            => await _projects.GetAsync(organizationId, projectId, cancellationToken) is { IsOrganisationArea: false } project
                ? project
                : throw new DomainException("Project not found");

        private async Task<(Domain.Organizations.Organization Organization, InterestedParty Party)> PartyAsync(Guid partyId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var party = (await _scopes.ListPartiesAsync(organization.Id, cancellationToken)).FirstOrDefault(p => p.Id == partyId && p.IsActive)
                ?? throw new DomainException("Interested party not found");
            return (organization, party);
        }

        private async Task EnsureUniqueAsync(Guid organizationId, InterestedParty party, CancellationToken cancellationToken)
        {
            if ((await _scopes.ListPartiesAsync(organizationId, cancellationToken))
                .Any(p => p.IsActive && p.Id != party.Id && string.Equals(p.Name, party.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"{party.Name} is already an interested party");
            }
        }
    }
}
