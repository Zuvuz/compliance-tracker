using System.Security.Cryptography;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-182: storage for the grants (implemented in Infrastructure)
    public interface IAuditorGrantRepository
    {
        Task<IReadOnlyList<AuditorGrant>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<AuditorGrant?> GetByInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

        Task AddAsync(AuditorGrant grant, CancellationToken cancellationToken = default);
    }

    // The link to send the auditor (shown once), and whether it was also sent by mail
    public sealed record GrantResult(string Token, bool Mailed);

    // US-182: an external auditor's read-only access to one project for a limited time
    public class AuditorAccessService
    {
        private readonly IAuditorGrantRepository _grants;
        private readonly IInvitationRepository _invitations;
        private readonly IProjectRepository _projects;
        private readonly IMailSender _mail;
        private readonly ReminderOptions _links;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AuditorAccessService(IAuditorGrantRepository grants, IInvitationRepository invitations, IProjectRepository projects, IMailSender mail,
            ReminderOptions links, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _grants = grants;
            _invitations = invitations;
            _projects = projects;
            _mail = mail;
            _links = links;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public static string LinkPath(string token) => $"/Invitations/Accept/{token}";

        // The project's grants, newest first; only for those who may decide compliance (R4)
        public async Task<IReadOnlyList<AuditorGrant>> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (!await _member.CanAsync(Permission.DecideCompliance, cancellationToken) || await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            return (await _grants.ListAsync(organization.Id, cancellationToken)).Where(g => g.ProjectId == projectId).OrderByDescending(g => g.GrantedAt).ToList();
        }

        // R1, R4: an invitation with the Auditor role, limited to the project and the dates when it is accepted
        public async Task<GrantResult> GrantAsync(Guid projectId, string? email, DateOnly? from, DateOnly? until, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsOrganisationArea)
            {
                throw new DomainException("Give access to a product's project");
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var invitation = Invitation.Create(organization.Id, UserRole.Auditor, MemberService.Hash(token), user.Id, Now);
            var grant = AuditorGrant.Create(organization.Id, project.Id, email, from, until, DateOnly.FromDateTime(Now), invitation.Id, user.Id, Now);
            await _invitations.AddAsync(invitation, cancellationToken);
            await _grants.AddAsync(grant, cancellationToken);
            await _log.LogAsync(ActivityAction.AuditorAccessGranted, project.Id,
                $"{grant.Email}: {grant.From:dd-MM-yyyy} – {grant.Until:dd-MM-yyyy}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // The link goes to the auditor by mail when a mail server is set up (US-183); it is always shown once as well
            var mailed = false;
            if (_mail.IsConfigured)
            {
                try
                {
                    var link = string.IsNullOrWhiteSpace(_links.BaseUrl) ? LinkPath(token) : _links.BaseUrl.TrimEnd('/') + LinkPath(token);
                    await _mail.SendAsync(grant.Email, $"Read-only access to {project.Name} for your audit",
                        $"""
                        {organization.Name} gives you read-only access to {project.Name} from {grant.From:dd-MM-yyyy} to {grant.Until:dd-MM-yyyy}.

                        Sign in and accept: {link}

                        The link works once and for {Invitation.ValidDays} days. Everything you open is logged.
                        """, cancellationToken);
                    mailed = true;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    mailed = false; // the link is shown to the person who granted it
                }
            }

            return new GrantResult(token, mailed);
        }

        // R3: at once, also when the invitation was not accepted yet
        public async Task RevokeAsync(Guid projectId, Guid grantId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var grant = (await _grants.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(g => g.Id == grantId && g.ProjectId == projectId)
                ?? throw new DomainException("Access not found");
            grant.Revoke(Now);
            if (grant.MemberId is { } memberId && organization.Members.Any(m => m.Id == memberId))
            {
                organization.EndAuditorAccess(memberId, user.Id, Now);
            }

            if (await _invitations.GetAsync(organization.Id, grant.InvitationId, cancellationToken) is { } invitation && invitation.IsUsable(Now))
            {
                invitation.Revoke(Now);
            }

            await _log.LogAsync(ActivityAction.AuditorAccessRevoked, projectId, grant.Email, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: every page and download the auditor opens
        public async Task LogUseAsync(Guid projectId, string what, CancellationToken cancellationToken = default)
        {
            await _log.LogAsync(ActivityAction.AuditorAccessUsed, projectId, what.Length <= 300 ? what : what[..300], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
