using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.WorkItems
{
    // US-109 R2, R3: a criterion the tracker checks from its own data. Manual criteria are ticked by a person (R4).
    public enum DoneCheck
    {
        Manual,
        TemplateFilled,     // user story: rules and examples; use case: primary actor, main success scenario, extensions; requirement: acceptance criteria
        Linked,             // linked to at least one requirement that applies
        Evidence,           // every linked requirement has proof that has not expired (US-204)
        TestsPassed,        // the linked requirements' tests passed, when test results are uploaded (US-058)
        NoOpenAiSuggestion  // no AI suggestion for it waits for a decision
    }

    // One criterion of a kind of work item: a check, or a manual criterion with its title
    public sealed record DoneCriterion(WorkItemType For, DoneCheck Check, string Title)
    {
        public bool IsManual => Check == DoneCheck.Manual;
    }

    // US-109 R1, R8: a project's Definition of Done: one list of criteria for each kind of work item. A project without one
    // uses the defaults (version 0); the first change stores them, and every change raises the version and is logged.
    public class DefinitionOfDone
    {
        public const int MaxTitleLength = 120;
        public const int MaxManual = 10;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public int Version { get; private set; }

        // Stored as one line per criterion, "TemplateFilled" or "Manual:Code reviewed"
        public string UserStoryCriteria { get; private set; } = "";
        public string UseCaseCriteria { get; private set; } = "";
        public string RequirementCriteria { get; private set; } = "";

        public Guid? ChangedById { get; private set; }
        public UserRole? ChangedRole { get; private set; }
        public DateTime? ChangedAt { get; private set; }

        // For EF Core only
        private DefinitionOfDone() { }

        // R1, R2: the defaults every project starts from
        public static DefinitionOfDone Default(Guid projectId)
        {
            var definition = new DefinitionOfDone { ProjectId = projectId };
            definition.UserStoryCriteria = Lines(
                [DoneCheck.TemplateFilled, DoneCheck.Linked, DoneCheck.Evidence, DoneCheck.TestsPassed], ["Code reviewed"]);
            definition.UseCaseCriteria = Lines([DoneCheck.TemplateFilled, DoneCheck.Linked, DoneCheck.Evidence], ["Accepted by the product owner"]);
            definition.RequirementCriteria = Lines([DoneCheck.TemplateFilled, DoneCheck.Linked], ["Verified"]);
            return definition;
        }

        public IReadOnlyList<DoneCriterion> For(WorkItemType kind)
            => Parse(kind, kind switch
            {
                WorkItemType.UseCase => UseCaseCriteria,
                WorkItemType.Requirement => RequirementCriteria,
                _ => UserStoryCriteria
            });

        public IEnumerable<DoneCriterion> All => Enum.GetValues<WorkItemType>().SelectMany(For);

        // R8: the checks and the manual criteria of one kind. Returns the old and new text for the log, or null when nothing changed.
        public (string Before, string After)? Change(WorkItemType kind, IEnumerable<DoneCheck> checks, IEnumerable<string> manual, Guid userId, UserRole? role, DateTime at)
        {
            var chosen = checks.Where(c => c != DoneCheck.Manual && Enum.IsDefined(c)).Distinct().OrderBy(c => c).ToList();
            var titles = manual.Select(m => (m ?? "").Trim()).Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (titles.Count > MaxManual)
            {
                throw new DomainException($"At most {MaxManual} manual criteria");
            }

            if (titles.Any(t => t.Length > MaxTitleLength))
            {
                throw new DomainException($"A criterion must be {MaxTitleLength} characters or fewer");
            }

            if (chosen.Count + titles.Count == 0)
            {
                throw new DomainException("Keep at least one criterion");
            }

            var before = Text(For(kind));
            var lines = Lines(chosen, titles);
            switch (kind)
            {
                case WorkItemType.UseCase: UseCaseCriteria = lines; break;
                case WorkItemType.Requirement: RequirementCriteria = lines; break;
                default: UserStoryCriteria = lines; break;
            }

            var after = Text(For(kind));
            if (before == after)
            {
                return null;
            }

            Version++;
            ChangedById = userId;
            ChangedRole = role;
            ChangedAt = at;
            return (before, after);
        }

        // e.g. "Template filled in, Linked to a requirement, Code reviewed"
        public static string Text(IEnumerable<DoneCriterion> criteria) => string.Join(", ", criteria.Select(c => c.Title));

        public static string TitleOf(DoneCheck check, WorkItemType kind) => check switch
        {
            DoneCheck.TemplateFilled => kind switch
            {
                WorkItemType.UseCase => "Primary actor, main success scenario and extensions filled in",
                WorkItemType.Requirement => "Acceptance criteria filled in",
                _ => "Rules and examples filled in"
            },
            DoneCheck.Linked => "Linked to at least one requirement",
            DoneCheck.Evidence => "Every linked requirement has evidence",
            DoneCheck.TestsPassed => "Tests passed",
            DoneCheck.NoOpenAiSuggestion => "No AI suggestion waits",
            _ => ""
        };

        private static string Lines(IEnumerable<DoneCheck> checks, IEnumerable<string> manual)
            => string.Join("\n", checks.Select(c => c.ToString()).Concat(manual.Select(m => $"Manual:{m}")));

        private static IReadOnlyList<DoneCriterion> Parse(WorkItemType kind, string lines)
            => lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.StartsWith("Manual:", StringComparison.Ordinal)
                    ? new DoneCriterion(kind, DoneCheck.Manual, line["Manual:".Length..])
                    : Enum.TryParse<DoneCheck>(line, out var check) ? new DoneCriterion(kind, check, TitleOf(check, kind)) : null)
                .OfType<DoneCriterion>()
                .ToList();
    }

    // US-109 R4: a person ticked a manual criterion on a work item; unticking keeps the row (history). A tick given before the
    // work item was reopened in GitHub no longer counts (decided by the user 19-11-2026).
    public class DoneTick
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid WorkItemId { get; private set; }
        public string Criterion { get; private set; } = "";
        public Guid TickedById { get; private set; }
        public UserRole? TickedRole { get; private set; }
        public DateTime TickedAt { get; private set; }
        public Guid? UntickedById { get; private set; }
        public DateTime? UntickedAt { get; private set; }

        public bool IsActive => UntickedAt is null;

        // For EF Core only
        private DoneTick() { }

        public static DoneTick Tick(Guid projectId, Guid workItemId, string criterion, Guid userId, UserRole? role, DateTime at)
        {
            var title = (criterion ?? "").Trim();
            if (title.Length == 0)
            {
                throw new DomainException("Choose the criterion");
            }

            return new DoneTick { ProjectId = projectId, WorkItemId = workItemId, Criterion = title, TickedById = userId, TickedRole = role, TickedAt = at };
        }

        public void Untick(Guid userId, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The criterion is not ticked");
            }

            UntickedById = userId;
            UntickedAt = at;
        }

        // Counts while it is ticked, and only if it was given after the work item was last reopened
        public bool CountsFor(WorkItem item) => IsActive && (item.ReopenedAt is not { } reopened || TickedAt > reopened);
    }
}
