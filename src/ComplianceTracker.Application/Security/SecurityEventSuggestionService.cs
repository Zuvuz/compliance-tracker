using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Security
{
    // US-128: a public list of exploited vulnerabilities (CISA KEV, the EU Vulnerability Database), read by Infrastructure
    public interface IExploitedVulnerabilityList
    {
        // The name the operator switches on, e.g. "KEV" or "EUVD"
        string Key { get; }

        // e.g. "CISA KEV"
        string Name { get; }

        // The CVE ids on the list. Throws when the list cannot be fetched.
        Task<IReadOnlyCollection<string>> FetchAsync(CancellationToken cancellationToken = default);
    }

    // US-128 R5: which lists the operator has switched on (Security:ExploitedLists:Enabled). None by default, so a
    // tracker without internet works as before.
    public interface IExploitedListSettings
    {
        IReadOnlyCollection<string> Enabled { get; }
    }

    public interface ISecurityEventSuggestionRepository
    {
        Task<IReadOnlyList<SecurityEventSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<SecurityEventSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default);

        Task AddAsync(IEnumerable<SecurityEventSuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    public interface IExploitedListRepository
    {
        Task<IReadOnlyList<ExploitedList>> ListAsync(CancellationToken cancellationToken = default);

        Task AddAsync(ExploitedList list, CancellationToken cancellationToken = default);
    }

    // What the Security events tab shows: the pending suggestions and how the switched-on lists were last fetched
    public record SuggestionOverview(IReadOnlyList<SecurityEventSuggestion> Pending, IReadOnlyList<ExploitedList> Lists);

    public class SecurityEventSuggestionService
    {
        private readonly IProjectRepository _projects;
        private readonly IScanRepository _scans;
        private readonly ISecurityEventRepository _events;
        private readonly ISecurityEventSuggestionRepository _suggestions;
        private readonly IExploitedListRepository _lists;
        private readonly IEnumerable<IExploitedVulnerabilityList> _sources;
        private readonly IExploitedListSettings _settings;
        private readonly SecurityEventService _eventService;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public SecurityEventSuggestionService(
            IProjectRepository projects, IScanRepository scans, ISecurityEventRepository events,
            ISecurityEventSuggestionRepository suggestions, IExploitedListRepository lists, IEnumerable<IExploitedVulnerabilityList> sources,
            IExploitedListSettings settings, SecurityEventService eventService, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _scans = scans;
            _events = events;
            _suggestions = suggestions;
            _lists = lists;
            _sources = sources;
            _settings = settings;
            _eventService = eventService;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // After a scan import (US-078): the findings as they are now, and the ones the scan added. Not saved here; the
        // import saves everything at once.
        public async Task MatchAsync(Project project, IReadOnlyList<Finding> findings, IReadOnlySet<Guid> newFindingIds, CancellationToken cancellationToken = default)
        {
            if (project.IsClosed)
            {
                return;
            }

            var exploited = await ExploitedAsync(cancellationToken);
            var existing = await _suggestions.ListAsync(project.Id, cancellationToken);
            var events = await _events.ListAsync(project.Id, cancellationToken);
            var added = SuggestionMatcher.Match(project.Id, findings, newFindingIds, exploited, events, existing, Now);
            if (added.Count == 0)
            {
                return;
            }

            await _suggestions.AddAsync(added, cancellationToken);
            foreach (var suggestion in added)
            {
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.SecurityEventSuggested, project.Id, Describe(suggestion), cancellationToken);
            }
        }

        // R5: fetches the switched-on lists (once a day, see Web/Background), then matches every project's open findings.
        // A list that cannot be fetched keeps its last version.
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            var enabled = _settings.Enabled.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stored = (await _lists.ListAsync(cancellationToken)).ToDictionary(l => l.Name);
            foreach (var source in _sources.Where(s => enabled.Contains(s.Key)))
            {
                if (!stored.TryGetValue(source.Name, out var list))
                {
                    list = new ExploitedList(source.Name);
                    await _lists.AddAsync(list, cancellationToken);
                    stored[source.Name] = list;
                }

                try
                {
                    list.Refreshed(await source.FetchAsync(cancellationToken), Now);
                }
                catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    list.Failed(error.Message, Now);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var projectId in await _scans.ListProjectIdsWithOpenFindingsAsync(cancellationToken))
            {
                if (await _projects.GetForSyncAsync(projectId, cancellationToken) is { } project)
                {
                    await MatchAsync(project, await _scans.ListFindingsAsync(project.Id, cancellationToken), new HashSet<Guid>(), cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The Security events tab and the Overview; null for other organisations' projects
        public async Task<SuggestionOverview?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var enabled = _settings.Enabled.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = _sources.Where(s => enabled.Contains(s.Key)).Select(s => s.Name).ToHashSet();
            var pending = (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.IsPending).OrderByDescending(s => s.Kind == SecurityEventKind.ActivelyExploitedVulnerability).ThenBy(s => s.LearnedAt).ToList();
            var lists = (await _lists.ListAsync(cancellationToken)).Where(l => names.Contains(l.Name)).OrderBy(l => l.Name, StringComparer.Ordinal).ToList();
            return new SuggestionOverview(pending, lists);
        }

        // R1: a person records the suggested event, aware since when the tracker learned it unless changed. For an
        // upgrade suggestion (R4) the event of US-127 is upgraded from that time.
        public async Task RecordAsync(Guid projectId, Guid suggestionId, DateTime? awareAtUtc, CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await _suggestions.GetAsync(project.Id, suggestionId, cancellationToken)
                ?? throw new DomainException("Suggestion not found");
            var awareAt = awareAtUtc ?? suggestion.LearnedAt;

            // Decided before the event is saved, so both are saved together
            suggestion.Accept(user.Id, Now);
            await _log.LogAsync(ActivityAction.SecurityEventSuggestionAccepted, project.Id, Describe(suggestion), cancellationToken);
            if (suggestion.UpgradeEventId is { } eventId)
            {
                await _eventService.UpgradeAsync(project.Id, eventId, awareAt, $"{suggestion.Key} is on {suggestion.SourceName}", cancellationToken);
                suggestion.LinkEvent(eventId);
            }
            else
            {
                var recorded = await _eventService.RecordAsync(
                    project.Id, suggestion.Kind, suggestion.Title, Description(suggestion), awareAt, cancellationToken);
                suggestion.LinkEvent(recorded.Id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6
        public async Task DismissAsync(Guid projectId, Guid suggestionId, string? reason, CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await _suggestions.GetAsync(project.Id, suggestionId, cancellationToken)
                ?? throw new DomainException("Suggestion not found");

            suggestion.Dismiss(reason, user.Id, Now);
            await _log.LogAsync(ActivityAction.SecurityEventSuggestionDismissed, project.Id, $"{Describe(suggestion)}: {suggestion.DismissedReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // On the tab and under "Needs attention", e.g. "CVE-2026-1234 is actively exploited and found in Acme Portal"
        public static string Text(SecurityEventSuggestion suggestion, string projectName) => suggestion switch
        {
            { IsUpgrade: true } => $"{suggestion.Key} is now exploited – upgrade \"{suggestion.Title}\"?",
            { Kind: SecurityEventKind.ActivelyExploitedVulnerability } => $"{suggestion.Key} is actively exploited and found in {projectName}",
            _ => $"New critical finding in {projectName}: {suggestion.Title}"
        };

        // In the log, e.g. "CVE-2026-1234 is actively exploited (CISA KEV)"
        public static string Describe(SecurityEventSuggestion suggestion) => suggestion switch
        {
            { IsUpgrade: true } => $"{suggestion.Key} is now exploited ({suggestion.SourceName}): upgrade \"{suggestion.Title}\"",
            { Kind: SecurityEventKind.ActivelyExploitedVulnerability } => $"{suggestion.Key} is actively exploited ({suggestion.SourceName})",
            _ => $"New critical finding {suggestion.Title} ({suggestion.SourceName})"
        };

        private static string Description(SecurityEventSuggestion suggestion) => suggestion.Source == SuggestionSource.ExploitedList
            ? $"Suggested by the tracker: {suggestion.Key} is on {suggestion.SourceName} and found by a scan of the project."
            : $"Suggested by the tracker: a new critical finding from {suggestion.SourceName}.";

        // CVE id → the first switched-on list it is on
        private async Task<IReadOnlyDictionary<string, string>> ExploitedAsync(CancellationToken cancellationToken)
        {
            var enabled = _settings.Enabled.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = _sources.Where(s => enabled.Contains(s.Key)).Select(s => s.Name).ToHashSet();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var list in (await _lists.ListAsync(cancellationToken)).Where(l => names.Contains(l.Name)).OrderBy(l => l.Name, StringComparer.Ordinal))
            {
                foreach (var cve in list.Cves)
                {
                    result.TryAdd(cve, list.Name);
                }
            }

            return result;
        }

        // R7: the same roles as US-073 (ManageRisks)
        private async Task<(User User, Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageRisks, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (user, project);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
