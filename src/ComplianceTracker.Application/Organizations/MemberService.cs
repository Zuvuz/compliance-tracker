using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Application.Organizations
{
    // Storage for invitations (implemented in Infrastructure)
    public interface IInvitationRepository
    {
        Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<Invitation?> GetAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Invitation>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);
        Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);
    }

    // What an invitation link shows before it is accepted
    public record InvitationPreview(string OrganizationName, UserRole Role, string? Problem);

    // US-009 and US-010: members, their roles and invitations. Only the organisation admin (ManageMembers).
    public class MemberService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly IInvitationRepository _invitations;
        private readonly ICurrentUser _currentUser;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public MemberService(
            IOrganizationRepository organizations,
            IInvitationRepository invitations,
            ICurrentUser currentUser,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _organizations = organizations;
            _invitations = invitations;
            _currentUser = currentUser;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-010 R2, R3, R6: only what the member may do from now on changes; their earlier decisions keep
        // the role they had then (every decision stores it)
        public async Task ChangeRoleAsync(Guid memberId, UserRole role, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var change = organization.ChangeRole(memberId, role, user.Id);
            if (change is not null)
            {
                await _log.LogAsync(ActivityAction.MemberRoleChanged, memberId, change.ToString(), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // US-009 R1, R6: a random token; only its SHA-256 is stored. Returns the token, which is shown once.
        public async Task<string> CreateInvitationAsync(UserRole role, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var invitation = Invitation.Create(organization.Id, role, Hash(token), user.Id, Now);
            await _invitations.AddAsync(invitation, cancellationToken);
            await _log.LogAsync(ActivityAction.InvitationCreated, invitation.Id, RolePermissions.Title(role), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return token;
        }

        // US-009 R4
        public async Task RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var invitation = await _invitations.GetAsync(organization.Id, invitationId, cancellationToken)
                ?? throw new DomainException("Invitation not found");

            invitation.Revoke(Now);
            await _log.LogAsync(ActivityAction.InvitationRevoked, invitation.Id, RolePermissions.Title(invitation.Role), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // What the link leads to, and why it cannot be used if it cannot. Null for an unknown link.
        public async Task<InvitationPreview?> PreviewAsync(string token, CancellationToken cancellationToken = default)
        {
            var invitation = await _invitations.GetByTokenHashAsync(Hash(token ?? ""), cancellationToken);
            if (invitation is null || await _organizations.GetByIdAsync(invitation.OrganizationId, cancellationToken) is not { } organization)
            {
                return null;
            }

            string? problem = null;
            try
            {
                invitation.EnsureUsable(Now);
                if (await _organizations.GetByMemberAsync(_currentUser.Identity.ExternalId, cancellationToken) is not null)
                {
                    problem = "You are already a member of an organisation";
                }
            }
            catch (DomainException error)
            {
                problem = error.Message;
            }

            return new InvitationPreview(organization.Name, invitation.Role, problem);
        }

        // US-009 R2, R5, R7: the signed-in person joins with the invitation's role
        public async Task AcceptInvitationAsync(string token, CancellationToken cancellationToken = default)
        {
            var identity = _currentUser.Identity;
            var invitation = await _invitations.GetByTokenHashAsync(Hash(token ?? ""), cancellationToken)
                ?? throw new DomainException("The invitation was not found");
            invitation.EnsureUsable(Now);

            // B16: one organisation per person
            if (await _organizations.GetByMemberAsync(identity.ExternalId, cancellationToken) is not null)
            {
                throw new DomainException("You are already a member of an organisation");
            }

            var organization = await _organizations.GetByIdAsync(invitation.OrganizationId, cancellationToken)
                ?? throw new DomainException("The invitation was not found");

            var user = organization.AddMember(identity, invitation.Role);
            invitation.MarkAccepted(user.Id, Now);
            await _log.LogAsync(organization.Id, user.Id, ActivityAction.InvitationAccepted, invitation.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _member.Reset();
        }

        public static string Hash(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // What the members page reads: the organisation's open invitations (US-009)
    public class MemberQueries
    {
        private readonly IInvitationRepository _invitations;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public MemberQueries(IInvitationRepository invitations, CurrentMember member, TimeProvider clock)
        {
            _invitations = invitations;
            _member = member;
            _clock = clock;
        }

        // Only for those who manage members; others get an empty list
        public async Task<IReadOnlyList<Invitation>> OpenInvitationsAsync(CancellationToken cancellationToken = default)
        {
            if (!await _member.CanAsync(Permission.ManageMembers, cancellationToken)
                || await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            return (await _invitations.ListAsync(organization.Id, cancellationToken)).Where(i => i.IsUsable(now)).ToList();
        }
    }

    // US-010: the tracker's owner (Keycloak realm role "system-admin"). Sees organisations and their members and can
    // change roles, e.g. when an organisation has locked itself out. Cannot see the organisations' compliance data.
    public class SystemAdminService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public SystemAdminService(IOrganizationRepository organizations, ICurrentUser currentUser, IActivityLogger log, IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            return await _organizations.ListAllAsync(cancellationToken);
        }

        public async Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            return await _organizations.GetByIdAsync(organizationId, cancellationToken);
        }

        // Logged in the organisation's own log as the system, with the system admin's name
        public async Task ChangeRoleAsync(Guid organizationId, Guid memberId, UserRole role, CancellationToken cancellationToken = default)
        {
            EnsureSystemAdmin();
            var organization = await _organizations.GetByIdAsync(organizationId, cancellationToken)
                ?? throw new DomainException("Organisation not found");

            var change = organization.ChangeRoleAsSystemAdmin(memberId, role);
            if (change is not null)
            {
                await _log.LogSystemAsync(organization.Id, ActivityAction.MemberRoleChanged, memberId,
                    $"System admin {_currentUser.Identity.Name}: {change}", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        private void EnsureSystemAdmin()
        {
            if (!_currentUser.IsSystemAdmin)
            {
                throw new DomainException("Only the system admin can do this");
            }
        }
    }
}
