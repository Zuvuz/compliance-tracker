using System.Security.Cryptography;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-184: every table of one organisation (implemented in Infrastructure from the data model)
    public interface IOrganisationData
    {
        Task<byte[]> ExportAsync(Guid organizationId, string organisationName, DateTime at, CancellationToken cancellationToken = default);

        // Everything but the activity log, the organisation's row and its members (anonymised by the domain)
        Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, long>> CountAsync(Guid organizationId, CancellationToken cancellationToken = default);
    }

    public sealed record OrganisationExport(string FileName, byte[] Content, string Sha256);

    // US-184: the organisation's way out. R1: the organisation admin only. R2: all data in open formats. R3: closing waits
    // 30 days and can be undone; then everything is deleted and the organisation and its members are anonymised.
    public class OrganisationExitService
    {
        private readonly IOrganisationData _data;
        private readonly IOrganizationRepository _organizations;
        private readonly CurrentMember _member;
        private readonly IMailSender _mail;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public OrganisationExitService(IOrganisationData data, IOrganizationRepository organizations, CurrentMember member, IMailSender mail,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _data = data;
            _organizations = organizations;
            _member = member;
            _mail = mail;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // R1, R2
        public async Task<OrganisationExport> ExportAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var content = await _data.ExportAsync(organization.Id, organization.Name, Now, cancellationToken);
            var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            await _log.LogAsync(ActivityAction.OrganisationExported, organization.Id, $"SHA-256 {sha}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new OrganisationExport($"{Frameworks.FrameworkSlugs.Slug(organization.Name)}-all-data-{Today:yyyy-MM-dd}.zip", content, sha);
        }

        // R1, R3, R4: the name is typed to confirm; the members are told by mail
        public async Task StartClosingAsync(string? confirmName, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            if (!string.Equals((confirmName ?? "").Trim(), organization.Name, StringComparison.Ordinal))
            {
                throw new DomainException("Type the organisation's name to confirm");
            }

            organization.StartClosing(user.Id, Today);
            await _log.LogAsync(ActivityAction.OrganisationClosingStarted, organization.Id, $"Deleted on {organization.ClosesOn:dd-MM-yyyy} unless undone", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_mail.IsConfigured)
            {
                foreach (var member in organization.Members)
                {
                    try
                    {
                        await _mail.SendAsync(member.Email, $"{organization.Name} is being closed",
                            $"""
                            {user.Name} has started closing {organization.Name} in the compliance tracker.
                            All its data is deleted on {organization.ClosesOn:dd-MM-yyyy}, unless an organisation admin undoes it before then.
                            Download what you need before that day.
                            """, cancellationToken);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        // A member who cannot be mailed still sees the notice in the tracker
                    }
                }
            }
        }

        // R3
        public async Task UndoClosingAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            organization.UndoClosing(user.Id);
            await _log.LogAsync(ActivityAction.OrganisationClosingUndone, organization.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3: run by the hourly job; the organisations whose waiting period is over are deleted and anonymised
        public async Task<int> CloseDueAsync(CancellationToken cancellationToken = default)
        {
            var closed = 0;
            foreach (var due in (await _organizations.ListAllAsync(cancellationToken)).Where(o => o.IsClosingDue(Today)).ToList())
            {
                await _data.DeleteAsync(due.Id, cancellationToken);
                var organization = await _organizations.GetByIdAsync(due.Id, cancellationToken) ?? due;
                organization.Close(Now);
                await _log.LogSystemAsync(organization.Id, ActivityAction.OrganisationClosed, organization.Id, "All data deleted; the organisation and its members anonymised", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                closed++;
            }

            return closed;
        }
    }
}
