using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Projects
{
    // US-192: storage for the organisation's own checklist items (implemented in Infrastructure)
    public interface IOrganisationChecklistRepository
    {
        // Removed ones too, so ticks made on them keep their text
        Task<IReadOnlyList<OrganisationChecklistItem>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(OrganisationChecklistItem item, CancellationToken cancellationToken = default);
    }

    // The Checklists page: per phase the built-in items (read only) and the organisation's own
    public sealed record OrganisationChecklistsView(IReadOnlyList<ChecklistItem> BuiltIn, IReadOnlyList<OrganisationChecklistItem> Own,
        IReadOnlyList<(Guid Id, string Name)> Frameworks, bool CanManage)
    {
        // US-193: the frameworks the organisation uploaded itself, which the AI can suggest checklist items for
        public IReadOnlyList<(Guid Id, string Name)> OwnFrameworks { get; init; } = [];
    }

    public class OrganisationChecklistService
    {
        private readonly IOrganisationChecklistRepository _items;
        private readonly IFrameworkRepository _frameworks;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public OrganisationChecklistService(IOrganisationChecklistRepository items, IFrameworkRepository frameworks, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _items = items;
            _frameworks = frameworks;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public async Task<OrganisationChecklistsView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var all = await _frameworks.ListAsync(organization.Id, cancellationToken);
            var frameworks = all.Select(f => (f.Id, FrameworkSlugs.Short(f.Name))).OrderBy(f => f.Item2, StringComparer.OrdinalIgnoreCase).ToList();
            return new OrganisationChecklistsView(BuiltInChecklist.All,
                (await _items.ListAsync(organization.Id, cancellationToken)).Where(i => i.IsActive).OrderBy(i => i.Phase).ThenBy(i => i.Text).ToList(),
                frameworks, user.Can(Permission.ManageFrameworks))
            {
                OwnFrameworks = all.Where(f => f.OwnerOrganizationId == organization.Id)
                    .Select(f => (f.Id, FrameworkSlugs.Short(f.Name))).OrderBy(f => f.Item2, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        // R1, R2, R6
        public async Task AddAsync(LifecyclePhase? phase, string? text, Guid? frameworkId, string? reference, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            string? source = null;
            if (frameworkId is { } id)
            {
                var framework = (await _frameworks.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(f => f.Id == id)
                    ?? throw new DomainException("Choose one of the organisation's frameworks");
                source = FrameworkSlugs.Short(framework.Name);
            }

            var item = OrganisationChecklistItem.Create(organization.Id, phase, text, frameworkId, source, reference, Now);
            if ((await _items.ListAsync(organization.Id, cancellationToken)).Any(i => i.IsActive && i.Phase == item.Phase
                    && string.Equals(i.Text, item.Text, StringComparison.OrdinalIgnoreCase) && i.FrameworkId == item.FrameworkId))
            {
                throw new DomainException("The phase already has this item");
            }

            await _items.AddAsync(item, cancellationToken);
            await _log.LogAsync(ActivityAction.OrganisationChecklistItemAdded, organization.Id, $"{item.Phase}: {item.Text} ({item.Source})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ChangeAsync(Guid itemId, string? text, string? reference, CancellationToken cancellationToken = default)
        {
            var (organization, item) = await OpenAsync(itemId, cancellationToken);
            item.Change(text, reference);
            await _log.LogAsync(ActivityAction.OrganisationChecklistItemChanged, organization, $"{item.Phase}: {item.Text} ({item.Source})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: hidden from new checklists; ticks stay
        public async Task RemoveAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            var (organization, item) = await OpenAsync(itemId, cancellationToken);
            item.Remove(Now);
            await _log.LogAsync(ActivityAction.OrganisationChecklistItemRemoved, organization, $"{item.Phase}: {item.Text} ({item.Source})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(Guid Organization, OrganisationChecklistItem Item)> OpenAsync(Guid itemId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var item = (await _items.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(i => i.Id == itemId && i.IsActive)
                ?? throw new DomainException("Checklist item not found");
            return (organization.Id, item);
        }
    }
}
