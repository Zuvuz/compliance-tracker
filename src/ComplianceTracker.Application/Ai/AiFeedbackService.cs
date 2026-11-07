using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Application.Ai
{
    // US-036 R5–R9: the organisation's consent to use its decisions to improve the AI, and the export.
    public class AiFeedbackService
    {
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly IProjectRepository _projects;

        public AiFeedbackService(
            IMappingSuggestionRepository suggestions,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            IProjectRepository projects)
        {
            _clock = clock;
            _projects = projects;
            _suggestions = suggestions;
            _frameworks = frameworks;
            _workItems = workItems;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // R5, R6: only an Admin; every change is logged
        public async Task SetSharingAsync(bool share, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageAiSharing, cancellationToken);
            if (organization.SetShareAiFeedback(share, user.Id, _clock.GetUtcNow().UtcDateTime))
            {
                await _log.LogAsync(ActivityAction.AiFeedbackSharingChanged, organization.Id, share ? "Sharing on" : "Sharing off", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // R7–R9: the decided suggestions as JSON Lines, without names or e-mails. Only for an Admin, only while sharing is on.
        // US-130 R6: how many of the organisation's decisions may be used to improve the AI; organisations cannot export
        // them for their own models (only the system owner trains, US-123)
        public async Task<int> SharedCountAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageAiSharing, cancellationToken);
            var excluded = (await _projects.ListAsync(organization.Id, cancellationToken))
                .Where(p => p.ExcludedFromAiSharing).Select(p => p.Id).ToHashSet();
            return (await _suggestions.ListDecidedAsync(organization.Id, cancellationToken)).Count(d => AiSharing.MayUse(organization, excluded, d));
        }
    }
}
