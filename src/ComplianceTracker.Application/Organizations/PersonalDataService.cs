using System.Text.Json;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-095: a member's own data (R2), and the erasure of a removed member's personal data (R3). Both are logged without
    // the personal data itself (R6).
    public class PersonalDataService
    {
        public const int MaxEntries = 100_000;

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private readonly CurrentMember _member;
        private readonly IActivityLogRepository _log;
        private readonly IActivityLogger _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public PersonalDataService(CurrentMember member, IActivityLogRepository log, IActivityLogger logger, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _member = member;
            _log = log;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R2: the profile, the organisation and the role, and every log entry the member made, as JSON
        public async Task<byte[]> ExportMineAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var (entries, _) = await _log.SearchAsync(new ActivityLogSearch(organization.Id, null, null, [], null, null, null, user.Id, false,
                ActivitySort.Oldest, 0, MaxEntries), cancellationToken);

            var export = new
            {
                madeAt = _clock.GetUtcNow().UtcDateTime,
                profile = new { user.Name, user.Email, keycloakId = user.ExternalId, role = user.Role.ToString(), user.SignsForManagement },
                organisation = new { organization.Name, organization.Cvr },
                activity = entries.Select(e => new { time = e.OccurredAt, action = e.Action.ToString(), role = e.Role?.ToString(), details = e.Details })
            };

            await _logger.LogAsync(ActivityAction.PersonalDataExported, user.Id, $"{entries.Count} log entries", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return JsonSerializer.SerializeToUtf8Bytes(export, Json);
        }

        // R3, R4: the Admin (ManageMembers) on a removed member; the history is kept under the pseudonym
        public async Task<User> EraseAsync(Guid memberId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var erased = organization.ErasePersonalData(memberId, user.Id);
            await _logger.LogAsync(ActivityAction.PersonalDataErased, erased.Id, erased.Pseudonym, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return erased;
        }
    }
}
