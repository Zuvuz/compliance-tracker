using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Ai
{
    // Storage for the assessments asked for (implemented in Infrastructure)
    public interface INeedsActionReassessmentRepository
    {
        // The one that has not run yet, if any
        Task<NeedsActionReassessment?> OpenAsync(Guid projectId, CancellationToken cancellationToken = default);

        // The newest one, for the Compliance tab
        Task<NeedsActionReassessment?> LatestAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<NeedsActionReassessment>> DueAsync(DateTime now, int max, CancellationToken cancellationToken = default);

        Task AddAsync(NeedsActionReassessment reassessment, CancellationToken cancellationToken = default);
    }

    // US-156: asks for a new assessment when the product changes (R1), and runs the ones that are due in the background
    // (R2). The callers save the request with their own change.
    public class NeedsActionReassessments
    {
        private readonly INeedsActionReassessmentRepository _requests;
        private readonly TimeProvider _clock;

        public NeedsActionReassessments(INeedsActionReassessmentRepository requests, TimeProvider clock)
        {
            _requests = requests;
            _clock = clock;
        }

        // R1, R2, R5: one waiting request per project; another change only adds its reason and waits again
        public async Task RequestAsync(Project project, string reason, CancellationToken cancellationToken = default)
        {
            if (project.IsClosed)
            {
                return;
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            if (await _requests.OpenAsync(project.Id, cancellationToken) is { } open)
            {
                open.Again(reason, now);
            }
            else
            {
                await _requests.AddAsync(NeedsActionReassessment.Request(project.OrganizationId, project.Id, reason, now), cancellationToken);
            }
        }
    }

    // US-156 R2, R4: the background job's work: the due requests, each in its own scope
    public class NeedsActionReassessmentJob
    {
        // How many projects one run assesses
        public const int PerRun = 5;

        private readonly INeedsActionReassessmentRepository _requests;
        private readonly IReassessmentScopes _scopes;
        private readonly TimeProvider _clock;

        public NeedsActionReassessmentJob(INeedsActionReassessmentRepository requests, IReassessmentScopes scopes, TimeProvider clock)
        {
            _requests = requests;
            _scopes = scopes;
            _clock = clock;
        }

        // R2, R4: runs the due requests, each in its own scope (like a request). When the AI is down, nothing of that
        // assessment is saved; a fresh scope marks the request as waiting, and it is tried again next time.
        // Returns how many were finished.
        public async Task<int> RunDueAsync(CancellationToken cancellationToken = default)
        {
            var due = await _requests.DueAsync(_clock.GetUtcNow().UtcDateTime, PerRun, cancellationToken);
            var finished = 0;
            foreach (var request in due)
            {
                var outcome = await _scopes.RunAsync((runner, token) => runner.RunOneAsync(request.Id, token), cancellationToken);
                if (outcome == ReassessmentOutcome.WaitingForAi)
                {
                    await _scopes.RunAsync((runner, token) => runner.MarkWaitingAsync(request.Id, token), cancellationToken);
                }
                else if (outcome == ReassessmentOutcome.Done)
                {
                    finished++;
                }
            }

            return finished;
        }
    }

    public enum ReassessmentOutcome
    {
        Done,
        NotDue,
        WaitingForAi
    }

    // Runs one piece of background work in a new scope with its own DbContext (implemented in Web)
    public interface IReassessmentScopes
    {
        Task<ReassessmentOutcome> RunAsync(Func<NeedsActionReassessmentRunner, CancellationToken, Task<ReassessmentOutcome>> work, CancellationToken cancellationToken = default);
    }

    // One request, in its own scope
    public class NeedsActionReassessmentRunner
    {
        private readonly INeedsActionReassessmentRepository _requests;
        private readonly NeedsActionService _needsAction;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public NeedsActionReassessmentRunner(INeedsActionReassessmentRepository requests, NeedsActionService needsAction, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _requests = requests;
            _needsAction = needsAction;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<ReassessmentOutcome> RunOneAsync(Guid requestId, CancellationToken cancellationToken)
        {
            var request = await DueAsync(requestId, cancellationToken);
            if (request is null)
            {
                return ReassessmentOutcome.NotDue;
            }

            try
            {
                var result = await _needsAction.ReassessAsync(request.OrganizationId, request.ProjectId, request.Reasons, cancellationToken);
                request.Finish(result, _clock.GetUtcNow().UtcDateTime);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ReassessmentOutcome.Done;
            }
            catch (AiUnavailableException)
            {
                return ReassessmentOutcome.WaitingForAi; // R4: this scope is thrown away unsaved
            }
        }

        public async Task<ReassessmentOutcome> MarkWaitingAsync(Guid requestId, CancellationToken cancellationToken)
        {
            if (await DueAsync(requestId, cancellationToken) is { } request)
            {
                request.WaitForAi();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return ReassessmentOutcome.WaitingForAi;
        }

        private async Task<NeedsActionReassessment?> DueAsync(Guid requestId, CancellationToken cancellationToken)
            => (await _requests.DueAsync(_clock.GetUtcNow().UtcDateTime, int.MaxValue, cancellationToken)).FirstOrDefault(r => r.Id == requestId);
    }
}
