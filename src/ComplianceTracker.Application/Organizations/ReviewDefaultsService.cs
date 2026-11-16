using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-195: the default review dates the forms fill in (R1), and the organisation admin's change of them (R3)
    public class ReviewDefaultsService
    {
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ReviewDefaultsService(CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // The organisation's defaults, or the standard ones before it exists
        public async Task<ReviewDefaults> DefaultsAsync(CancellationToken cancellationToken = default)
            => (await _member.FindOrganizationAsync(cancellationToken))?.ReviewDefaults ?? ReviewDefaults.Standard;

        // R1: the date a form fills in
        public async Task<DateOnly> DateForAsync(ReviewDateKind kind, CancellationToken cancellationToken = default)
            => (await DefaultsAsync(cancellationToken)).DateFor(kind, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));

        public async Task SaveAsync(ReviewDefaults defaults, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            organization.SetReviewDefaults(defaults);
            await _log.LogAsync(ActivityAction.ReviewDefaultsChanged, organization.Id,
                $"Evidence {defaults.EvidenceValidity}, documents {defaults.DocumentReview}, supplier evidence {defaults.SupplierEvidence}, "
                + $"audit certificates {defaults.AuditCertificate} months", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
