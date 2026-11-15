using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Projects
{
    // US-047: storage for what a project decided about its checklist items (implemented in Infrastructure)
    public interface IProjectChecklistRepository
    {
        Task<IReadOnlyList<ProjectChecklistEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(ProjectChecklistEntry entry, CancellationToken cancellationToken = default);
    }

    // US-047: the current phase's checklist as the Overview shows it; the optional tests not chosen are offered apart (R9)
    public sealed record PhaseChecklistView(LifecyclePhase Phase, IReadOnlyList<ChecklistLine> Items, IReadOnlyList<ChecklistLine> Optional)
    {
        public int Done => Items.Count(i => i.State is ChecklistState.Done);

        // Turned off items do not count (R8)
        public int Counted => Items.Count(i => i.State is ChecklistState.Done or ChecklistState.Missing);

        public IReadOnlyList<ChecklistLine> Missing => Items.Where(i => i.State == ChecklistState.Missing).ToList();
    }

    // US-047 R4, R8, R9: ticking by hand, turning off with a reason, turning on. Those who may edit the project may.
    public class PhaseChecklistService
    {
        private readonly IProjectChecklistRepository _entries;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public PhaseChecklistService(IProjectChecklistRepository entries, IProjectRepository projects, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _entries = entries;
            _projects = projects;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public Task TickAsync(Guid projectId, string key, string? comment, string? evidenceUrl, CancellationToken cancellationToken = default)
            => ChangeAsync(projectId, key, (item, entry, user) =>
            {
                if (item.Check != ChecklistCheck.Manual)
                {
                    throw new DomainException("This item is checked by the tracker itself");
                }

                entry.Tick(user.Id, user.Role, comment, evidenceUrl, Now);
                return "done" + (entry.Comment is { } note ? $": {note}" : "");
            }, cancellationToken);

        public Task UntickAsync(Guid projectId, string key, CancellationToken cancellationToken = default)
            => ChangeAsync(projectId, key, (_, entry, _) =>
            {
                entry.Untick(Now);
                return "not done";
            }, cancellationToken);

        public Task TurnOffAsync(Guid projectId, string key, string? reason, CancellationToken cancellationToken = default)
            => ChangeAsync(projectId, key, (_, entry, _) =>
            {
                entry.TurnOff(reason, Now);
                return $"turned off: {entry.OffReason}";
            }, cancellationToken);

        public Task TurnOnAsync(Guid projectId, string key, CancellationToken cancellationToken = default)
            => ChangeAsync(projectId, key, (_, entry, _) =>
            {
                entry.TurnOn(Now);
                return "turned on";
            }, cancellationToken);

        private async Task ChangeAsync(Guid projectId, string key, Func<ChecklistItem, ProjectChecklistEntry, User, string> change,
            CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("The project is at end of support and cannot change");
            }

            var item = BuiltInChecklist.Find(key ?? "") ?? throw new DomainException("Checklist item not found");
            var entries = await _entries.ListAsync(project.Id, cancellationToken);
            var entry = entries.FirstOrDefault(e => e.ItemKey == item.Key);
            if (entry is null)
            {
                entry = ProjectChecklistEntry.For(project.Id, item.Key, Now);
                await _entries.AddAsync(entry, cancellationToken);
            }

            var what = change(item, entry, user);
            await _log.LogAsync(ActivityAction.ChecklistItemChanged, project.Id, $"{item.Text} ({item.Source}): {what}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
