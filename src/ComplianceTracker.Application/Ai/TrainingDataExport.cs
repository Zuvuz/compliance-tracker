using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Application.Ai
{
    // US-123 R6: the kinds of decision in an export
    public enum TrainingKind
    {
        Story, // US-020: a user story meets a requirement
        Evidence, // US-059: evidence proves a requirement
        NotApplicable // US-060: a requirement does not apply
    }

    // R6: the period is whole days, both included (UTC)
    public record TrainingDataRequest(DateOnly? From, DateOnly? To, IReadOnlySet<TrainingKind> Kinds)
    {
        public bool Includes(DateTime decidedAt)
        {
            var day = DateOnly.FromDateTime(decidedAt);
            return (From is not { } from || day >= from) && (To is not { } to || day <= to);
        }

        // e.g. "training-data-2026-10-01-to-2026-10-31.jsonl"
        public string FileName => $"training-data-{From?.ToString("yyyy-MM-dd") ?? "start"}-to-{To?.ToString("yyyy-MM-dd") ?? "now"}.jsonl";

        // e.g. "01-10-2026 to 31-10-2026: story, evidence"
        public string Describe() => $"{From?.ToString("dd-MM-yyyy") ?? "the start"} to {To?.ToString("dd-MM-yyyy") ?? "now"}: "
                                    + string.Join(", ", Kinds.Order().Select(k => Line.KindName(k)));
    }

    // R5: organisations and projects as "org-1" and "org-1/project-3", numbered in the order they are met, so the same
    // within one export and nothing in them points at a name
    public sealed class Pseudonyms
    {
        private readonly Dictionary<Guid, string> _organizations = new();
        private readonly Dictionary<Guid, string> _projects = new();
        private readonly Dictionary<Guid, int> _projectCount = new();

        public string Organization(Guid organizationId)
        {
            if (!_organizations.TryGetValue(organizationId, out var name))
            {
                name = $"org-{_organizations.Count + 1}";
                _organizations[organizationId] = name;
            }

            return name;
        }

        public string Project(Guid organizationId, Guid projectId)
        {
            if (!_projects.TryGetValue(projectId, out var name))
            {
                var count = _projectCount.GetValueOrDefault(organizationId) + 1;
                _projectCount[organizationId] = count;
                name = $"{Organization(organizationId)}/project-{count}";
                _projects[projectId] = name;
            }

            return name;
        }
    }

    // R4, GDPR Art. 5(1)(c): the members' names and every e-mail address are masked in the texts, e.g. a story that says
    // "Anna signs in" becomes "[person] signs in". Whole words only, so "Bo" does not touch "Board".
    public static partial class PersonMask
    {
        public const string Person = "[person]";
        public const string Email = "[e-mail]";

        public static string? Apply(string? text, IEnumerable<string> names)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var result = EmailPattern().Replace(text, Email);
            foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().OrderByDescending(n => n.Length))
            {
                result = System.Text.RegularExpressions.Regex.Replace(
                    result, $@"(?<!\w){System.Text.RegularExpressions.Regex.Escape(name)}(?!\w)", Person,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
            }

            return result;
        }

        // The full names and each part of them, e.g. "Anna Jensen" → "Anna Jensen", "Anna", "Jensen"; parts of one or two
        // letters are left, so "Bo" is masked only as a whole name
        public static IReadOnlyList<string> NamesOf(IEnumerable<string?> fullNames)
            => fullNames.Where(n => !string.IsNullOrWhiteSpace(n))
                .SelectMany(n => new[] { n!.Trim() }.Concat(n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 2)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        [System.Text.RegularExpressions.GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", System.Text.RegularExpressions.RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial System.Text.RegularExpressions.Regex EmailPattern();
    }

    // R4: one line of the file. No person's name, e-mail or id: only the texts, the decision and how the AI made it.
    public record Line(
        string Kind, string? Story, string? Evidence, string Requirement, string Decision, string? Reason, string? Comment,
        double? Score, double? Limit, string? EmbeddingModel, string? RationaleModel, string? Rationale, string Prompt,
        string Organisation, string Project, string SuggestedAt, string DecidedAt)
    {
        public static string KindName(TrainingKind kind) => kind switch
        {
            TrainingKind.Story => "story",
            TrainingKind.Evidence => "evidence",
            _ => "not-applicable"
        };

        // e.g. "approved", "rejected"
        public static string DecisionName(SuggestionStatus status) => status.ToString().ToLowerInvariant();

        // An empty text teaches nothing (R4 needs both texts)
        public static bool HasTexts(string? first, string? requirement) => !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(requirement);
    }

    // US-123: the system owner exports the decisions of the organisations that share them, as JSON Lines, to train or
    // tune the models outside the tracker. R3: this is the only way the decisions leave; nothing in the tracker learns
    // from them by itself.
    public class TrainingDataExport
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IOrganizationRepository _organizations;
        private readonly Projects.IProjectRepository _projects;
        private readonly IMappingSuggestionRepository _stories;
        private readonly IEvidenceLinkSuggestionRepository _evidenceLinks;
        private readonly IEvidenceRepository _evidence;
        private readonly IApplicabilitySuggestionRepository _applicability;
        private readonly IWorkItemRepository _workItems;
        private readonly IFrameworkRepository _frameworks;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public TrainingDataExport(
            IOrganizationRepository organizations, Projects.IProjectRepository projects, IMappingSuggestionRepository stories,
            IEvidenceLinkSuggestionRepository evidenceLinks, IEvidenceRepository evidence, IApplicabilitySuggestionRepository applicability,
            IWorkItemRepository workItems, IFrameworkRepository frameworks, ICurrentUser currentUser, IActivityLogger log,
            IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _projects = projects;
            _stories = stories;
            _evidenceLinks = evidenceLinks;
            _evidence = evidence;
            _applicability = applicability;
            _workItems = workItems;
            _frameworks = frameworks;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // R1: only the system admin; R6: a period that makes sense and at least one kind. Checked before anything is written.
        public void Validate(TrainingDataRequest request)
        {
            if (!_currentUser.IsSystemAdmin)
            {
                throw new DomainException("Only the system admin can export training data");
            }

            if (request.Kinds.Count == 0)
            {
                throw new DomainException("Choose at least one kind of decision");
            }

            if (request is { From: { } from, To: { } to } && from > to)
            {
                throw new DomainException("The period must start before it ends");
            }
        }

        // R6: "story", "evidence", "not-applicable"; unknown names are ignored
        public static IReadOnlySet<TrainingKind> Kinds(IEnumerable<string>? names)
            => (names ?? []).Select(n => Enum.GetValues<TrainingKind>().Cast<TrainingKind?>().FirstOrDefault(k => Line.KindName(k!.Value) == n?.Trim().ToLowerInvariant()))
                .OfType<TrainingKind>()
                .ToHashSet();

        // Writes one line per decision to output, as it goes, so a large export does not fill the memory. Returns how
        // many lines were written. R7: each included organisation's log says the system owner exported its decisions.
        public async Task<int> WriteAsync(TrainingDataRequest request, Stream output, CancellationToken cancellationToken = default)
        {
            Validate(request);
            var pseudonyms = new Pseudonyms();
            var requirements = new Dictionary<Guid, Requirement?>();
            var total = 0;
            await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

            // R2: in a fixed order, so the pseudonyms are the same for the same data
            foreach (var organization in (await _organizations.ListAllAsync(cancellationToken)).Where(o => o.ShareAiFeedback).OrderBy(o => o.Id))
            {
                var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).OrderBy(p => p.Id).ToList();
                var excluded = projects.Where(p => p.ExcludedFromAiSharing).Select(p => p.Id).ToHashSet();
                var names = PersonMask.NamesOf(organization.Users.Select(u => u.Name));
                var count = 0;

                async Task WriteLineAsync(Line line)
                {
                    var masked = line with
                    {
                        Story = PersonMask.Apply(line.Story, names),
                        Evidence = PersonMask.Apply(line.Evidence, names),
                        Requirement = PersonMask.Apply(line.Requirement, names)!,
                        Reason = PersonMask.Apply(line.Reason, names),
                        Comment = PersonMask.Apply(line.Comment, names),
                        Rationale = PersonMask.Apply(line.Rationale, names)
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(masked, Json).AsMemory(), cancellationToken);
                    count++;
                }

                async Task<string?> RequirementAsync(Guid id)
                {
                    if (!requirements.TryGetValue(id, out var requirement))
                    {
                        requirement = await _frameworks.GetRequirementAsync(id, cancellationToken);
                        requirements[id] = requirement;
                    }

                    return requirement is null ? null : MappingSuggestionService.RequirementText(requirement);
                }

                bool MayUse(Guid projectId, DateTime? decidedAt)
                    => decidedAt is { } at && AiSharing.MayUse(organization, excluded, projectId, at) && request.Includes(at);

                if (request.Kinds.Contains(TrainingKind.Story))
                {
                    var stories = new Dictionary<Guid, IReadOnlyList<Domain.WorkItems.WorkItem>>();
                    foreach (var s in (await _stories.ListDecidedAsync(organization.Id, cancellationToken))
                                 .Where(s => s.Status is SuggestionStatus.Approved or SuggestionStatus.Rejected && MayUse(s.ProjectId, s.DecidedAt))
                                 .OrderBy(s => s.DecidedAt))
                    {
                        if (!stories.TryGetValue(s.ProjectId, out var projectStories))
                        {
                            projectStories = await _workItems.ListAsync(s.ProjectId, cancellationToken);
                            stories[s.ProjectId] = projectStories;
                        }

                        var story = projectStories.FirstOrDefault(w => w.Id == s.WorkItemId) is { } w ? MappingSuggestionService.StoryText(w) : null;
                        var requirement = await RequirementAsync(s.RequirementId);
                        if (Line.HasTexts(story, requirement))
                        {
                            await WriteLineAsync(new Line(
                                "story", story, null, requirement!, Line.DecisionName(s.Status), s.RejectionReason?.ToString(), s.Comment,
                                s.Score, s.MinimumScore, s.EmbeddingModel, s.RationaleModel, s.Rationale, s.PromptVersion,
                                pseudonyms.Organization(organization.Id), pseudonyms.Project(organization.Id, s.ProjectId),
                                Day(s.CreatedAt), Day(s.DecidedAt!.Value)));
                        }
                    }
                }

                foreach (var project in projects.Where(p => !excluded.Contains(p.Id)))
                {
                    if (request.Kinds.Contains(TrainingKind.Evidence))
                    {
                        foreach (var s in (await _evidenceLinks.ListAsync(project.Id, cancellationToken))
                                     .Where(s => s.Status is SuggestionStatus.Approved or SuggestionStatus.Rejected && MayUse(s.ProjectId, s.DecidedAt))
                                     .OrderBy(s => s.DecidedAt))
                        {
                            var evidence = await _evidence.GetAsync(project.Id, s.EvidenceId, cancellationToken) is { } e ? EvidenceService.EvidenceText(e, null) : null;
                            var requirement = await RequirementAsync(s.RequirementId);
                            if (Line.HasTexts(evidence, requirement))
                            {
                                await WriteLineAsync(new Line(
                                    "evidence", null, evidence, requirement!, Line.DecisionName(s.Status), s.RejectionReason?.ToString(), s.Comment,
                                    s.Score, s.MinimumScore, s.EmbeddingModel, s.RationaleModel, s.Rationale, s.PromptVersion,
                                    pseudonyms.Organization(organization.Id), pseudonyms.Project(organization.Id, project.Id),
                                    Day(s.CreatedAt), Day(s.DecidedAt!.Value)));
                            }
                        }
                    }

                    if (request.Kinds.Contains(TrainingKind.NotApplicable))
                    {
                        foreach (var s in (await _applicability.ListAsync(project.Id, cancellationToken))
                                     .Where(s => s.Status is SuggestionStatus.Approved or SuggestionStatus.Rejected && MayUse(s.ProjectId, s.DecidedAt))
                                     .OrderBy(s => s.DecidedAt))
                        {
                            var requirement = await RequirementAsync(s.RequirementId);
                            if (Line.HasTexts(s.Reason, requirement))
                            {
                                // The AI's reason why it does not apply, against the requirement; the context it read is not the
                                // project's texts themselves, only which of them (e.g. "purpose, scope")
                                await WriteLineAsync(new Line(
                                    "not-applicable", null, null, requirement!, Line.DecisionName(s.Status), s.Reason, s.ContextUsed,
                                    null, null, null, s.Model, null, s.PromptVersion,
                                    pseudonyms.Organization(organization.Id), pseudonyms.Project(organization.Id, project.Id),
                                    Day(s.CreatedAt), Day(s.DecidedAt!.Value)));
                            }
                        }
                    }
                }

                if (count > 0)
                {
                    await _log.LogSystemAsync(organization.Id, ActivityAction.TrainingDataExported, organization.Id,
                        $"The system owner exported {count} decisions ({request.Describe()})", cancellationToken);
                    total += count;
                }
            }

            await writer.FlushAsync(cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return total;
        }

        private static string Day(DateTime at) => at.ToString("yyyy-MM-dd");
    }
}
