using System.Text.Json;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Approvals
{
    // Storage for approval requests (US-135, implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IApprovalRequestRepository
    {
        Task<ApprovalRequest?> GetAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken = default);

        // Every pending request of the organisation, oldest first
        Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // The project's requests, newest first
        Task<IReadOnlyList<ApprovalRequest>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
    }

    // US-135: does an approved action. Each action that needs four-eyes has one; the use case that owns the action
    // implements it, so the action is done by the same code whether or not it waited for approval.
    public interface IApprovalExecutor
    {
        ApprovalAction Action { get; }

        // R3: who may approve: the permission the action itself needs
        Permission Permission { get; }

        // R4: why the request no longer fits its subject, or null when the subject is as it was
        Task<string?> WhyStaleAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

        // Done as the person who asked, after another person approved; logged as the person who asked
        Task ExecuteAsync(ApprovalRequest request, Organization organization, User requester, User approver, CancellationToken cancellationToken = default);
    }

    // US-135: where a use case asks for approval instead of acting at once. It does not save; the use case does.
    public class ApprovalGate
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IApprovalRequestRepository _requests;
        private readonly IActivityLogger _log;
        private readonly TimeProvider _clock;

        public ApprovalGate(IApprovalRequestRepository requests, IActivityLogger log, TimeProvider clock)
        {
            _requests = requests;
            _log = log;
            _clock = clock;
        }

        public static T Read<T>(ApprovalRequest request)
            => JsonSerializer.Deserialize<T>(request.Payload, Json) ?? throw new DomainException("The request cannot be read");

        public async Task<ApprovalRequest> RequestAsync(
            Organization organization, User requester, Guid? projectId, ApprovalAction action, Guid? subjectId, string? subjectStamp,
            string summary, object payload, CancellationToken cancellationToken = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            if (subjectId is not null && (await _requests.ListPendingAsync(organization.Id, cancellationToken))
                    .Any(r => r.Action == action && r.SubjectId == subjectId && !r.IsDue(now)))
            {
                throw new DomainException("This already waits for approval");
            }

            var request = ApprovalRequest.Create(
                organization.Id, projectId, action, subjectId, subjectStamp, summary, JsonSerializer.Serialize(payload, Json), requester.Id, now);
            await _requests.AddAsync(request, cancellationToken);
            await _log.LogAsAsync(organization.Id, requester, ActivityAction.ApprovalRequested, projectId ?? subjectId, request.Summary, cancellationToken);
            return request;
        }
    }

    // A request as a page shows it
    public record ApprovalView(
        ApprovalRequest Request, string RequestedBy, string? DecidedBy, bool CanApprove, bool CanWithdraw, bool NoOneElseMayApprove);

    // US-135: approve, reject or withdraw a request, and the lists for "Needs attention" and the project
    public class ApprovalService
    {
        private readonly IApprovalRequestRepository _requests;
        private readonly IEnumerable<IApprovalExecutor> _executors;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ApprovalService(
            IApprovalRequestRepository requests, IEnumerable<IApprovalExecutor> executors, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _requests = requests;
            _executors = executors;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // R3, R4, R5: another person with the action's permission; the action is done in the same save
        public async Task ApproveAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            var (organization, user, request, executor) = await OpenAsync(requestId, cancellationToken);
            if (!user.Can(executor.Permission))
            {
                throw new DomainException("Your role does not allow this");
            }

            if (user.Id == request.RequestedById)
            {
                throw new DomainException("Another person must approve this");
            }

            await EnsureOpenAsync(organization, request, executor, cancellationToken);

            var requester = organization.Users.First(u => u.Id == request.RequestedById);
            request.Approve(user.Id, Now);
            await _log.LogAsync(ActivityAction.ApprovalGiven, request.ProjectId ?? request.SubjectId, $"{request.Summary} (asked by {requester.Name})", cancellationToken);
            await executor.ExecuteAsync(request, organization, requester, user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(Guid requestId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, user, request, executor) = await OpenAsync(requestId, cancellationToken);
            if (!user.Can(executor.Permission))
            {
                throw new DomainException("Your role does not allow this");
            }

            await EnsureOpenAsync(organization, request, executor, cancellationToken);
            request.Reject(user.Id, reason, Now);
            await _log.LogAsync(ActivityAction.ApprovalRejected, request.ProjectId ?? request.SubjectId, $"{request.Summary}: {request.DecisionNote}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task WithdrawAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            var (_, user, request, _) = await OpenAsync(requestId, cancellationToken);
            request.Withdraw(user.Id, Now);
            await _log.LogAsync(ActivityAction.ApprovalWithdrawn, request.ProjectId ?? request.SubjectId, request.Summary, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // "Needs attention": the requests the member may approve, and the member's own that wait. Expired ones are left out.
        public async Task<IReadOnlyList<ApprovalView>> PendingAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            return (await _requests.ListPendingAsync(organization.Id, cancellationToken))
                .Where(r => !r.IsDue(Now) && (projectId is null || r.ProjectId == projectId))
                .Select(r => View(organization, user, r))
                .Where(v => v.CanApprove || v.CanWithdraw)
                .ToList();
        }

        // The project's requests, decided ones too, newest first; null for other organisations' projects
        public async Task<IReadOnlyList<ApprovalView>> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            return (await _requests.ListForProjectAsync(projectId, cancellationToken))
                .Where(r => r.OrganizationId == organization.Id)
                .Select(r => View(organization, user, r))
                .ToList();
        }

        // R3: whether someone other than the person who asked may approve; when no one may, the page says so
        public static bool AnyoneElseMayApprove(Organization organization, Guid requestedById, Permission permission)
            => organization.Members.Any(u => u.Id != requestedById && u.Can(permission));

        public Permission PermissionFor(ApprovalAction action) => Executor(action).Permission;

        private ApprovalView View(Organization organization, User user, ApprovalRequest request)
        {
            var open = request.IsPending && !request.IsDue(Now);
            return new ApprovalView(
                request,
                organization.Users.FirstOrDefault(u => u.Id == request.RequestedById)?.Name ?? "?",
                request.DecidedById is { } id ? organization.Users.FirstOrDefault(u => u.Id == id)?.Name : null,
                open && user.Id != request.RequestedById && user.Can(Executor(request.Action).Permission),
                open && user.Id == request.RequestedById,
                open && !AnyoneElseMayApprove(organization, request.RequestedById, Executor(request.Action).Permission));
        }

        // R4, R5: an expired or stale request is closed and saved, and the member is told why
        private async Task EnsureOpenAsync(Organization organization, ApprovalRequest request, IApprovalExecutor executor, CancellationToken cancellationToken)
        {
            if (!request.IsPending)
            {
                throw new DomainException("The request is decided already");
            }

            if (request.ExpireIfDue(Now))
            {
                await _log.LogSystemAsync(organization.Id, ActivityAction.ApprovalExpired, request.ProjectId ?? request.SubjectId, request.Summary, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new DomainException($"The request expired after {ApprovalRequest.ExpiresAfter.Days} days; ask again");
            }

            if (await executor.WhyStaleAsync(request, cancellationToken) is { } why)
            {
                request.Cancel(why, Now);
                await _log.LogSystemAsync(organization.Id, ActivityAction.ApprovalCancelled, request.ProjectId ?? request.SubjectId, $"{request.Summary}: {why}", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new DomainException($"The request was cancelled: {why}. Ask again if it is still needed.");
            }
        }

        private async Task<(Organization Organization, User User, ApprovalRequest Request, IApprovalExecutor Executor)> OpenAsync(
            Guid requestId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var request = await _requests.GetAsync(organization.Id, requestId, cancellationToken)
                ?? throw new DomainException("Request not found");
            return (organization, user, request, Executor(request.Action));
        }

        private IApprovalExecutor Executor(ApprovalAction action)
            => _executors.FirstOrDefault(e => e.Action == action)
               ?? throw new InvalidOperationException($"No executor for {action}");
    }
}
