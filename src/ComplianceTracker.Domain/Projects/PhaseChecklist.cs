using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Projects
{
    // US-047 R4: how an item is checked: by a person, or from what the tracker already knows
    public enum ChecklistCheck
    {
        Manual,
        Scope,
        Classification,
        Risks,
        Coverage,
        Evidence,
        Findings,
        Sbom,
        Support,
        Retained
    }

    // US-047: one built-in item of a phase's checklist. Regulation is what the badge says ("CRA", "ISO/IEC 27001", "AI Act",
    // or "Optional test" for the tests a project chooses itself, R9).
    public sealed record ChecklistItem(string Key, LifecyclePhase Phase, string Regulation, string Reference, string Text, ChecklistCheck Check,
        bool Optional = false)
    {
        // e.g. "CRA Art. 13(8)"
        public string Source => Reference.Length == 0 ? Regulation : $"{Regulation} {Reference}";
    }

    // US-047 R2: the built-in items per phase and regulation. An item is shown for a project that follows its regulation;
    // the optional tests are offered to every project.
    public static class BuiltInChecklist
    {
        public const string Cra = "CRA";
        public const string Iso27001 = "ISO/IEC 27001";
        public const string AiAct = "AI Act";
        public const string OptionalTest = "Optional test";

        public static readonly IReadOnlyList<ChecklistItem> All =
        [
            // Planning
            new("cra-scope", LifecyclePhase.Planning, Cra, "Annex VII(1)", "Intended purpose and scope described", ChecklistCheck.Scope),
            new("cra-class", LifecyclePhase.Planning, Cra, "Art. 7–8", "Classified under the CRA", ChecklistCheck.Classification),
            new("cra-risk", LifecyclePhase.Planning, Cra, "Art. 13(2)", "First cybersecurity risk assessment made", ChecklistCheck.Risks),
            new("iso-project", LifecyclePhase.Planning, Iso27001, "A.5.8", "Information security is part of the project's plan", ChecklistCheck.Manual),
            new("iso-risk", LifecyclePhase.Planning, Iso27001, "6.1.2", "Risks assessed", ChecklistCheck.Risks),
            new("ai-class", LifecyclePhase.Planning, AiAct, "Art. 6", "Classified under the AI Act", ChecklistCheck.Classification),

            // Design
            new("cra-secure-design", LifecyclePhase.Design, Cra, "Annex I Part I", "Essential requirements designed in (secure by default, small attack surface)", ChecklistCheck.Manual),
            new("cra-threat-model", LifecyclePhase.Design, Cra, "Art. 13(3)", "Threat model made", ChecklistCheck.Manual),
            new("iso-requirements", LifecyclePhase.Design, Iso27001, "A.8.26", "Security requirements specified", ChecklistCheck.Manual),
            new("iso-architecture", LifecyclePhase.Design, Iso27001, "A.8.27", "Secure architecture principles applied", ChecklistCheck.Manual),
            new("ai-risk-management", LifecyclePhase.Design, AiAct, "Art. 9", "Risk management system set up", ChecklistCheck.Manual),
            new("ai-data", LifecyclePhase.Design, AiAct, "Art. 10", "Data governance for training, validation and test data", ChecklistCheck.Manual),
            new("ai-oversight", LifecyclePhase.Design, AiAct, "Art. 14", "Human oversight designed", ChecklistCheck.Manual),

            // Development
            new("cra-coverage", LifecyclePhase.Development, Cra, "Annex I", "Every applicable requirement covered", ChecklistCheck.Coverage),
            new("cra-vulnerability-handling", LifecyclePhase.Development, Cra, "Annex I Part II", "Vulnerability handling process in place", ChecklistCheck.Manual),
            new("iso-coding", LifecyclePhase.Development, Iso27001, "A.8.28", "Secure coding rules followed", ChecklistCheck.Manual),
            new("iso-environments", LifecyclePhase.Development, Iso27001, "A.8.31", "Development, test and production separated", ChecklistCheck.Manual),
            new("ai-logging", LifecyclePhase.Development, AiAct, "Art. 12", "Logging built in", ChecklistCheck.Manual),
            new("ai-documentation", LifecyclePhase.Development, AiAct, "Art. 11", "Technical documentation kept up to date", ChecklistCheck.Manual),

            // Verification
            new("cra-sbom", LifecyclePhase.Verification, Cra, "Annex I Part II(1)", "SBOM uploaded", ChecklistCheck.Sbom),
            new("cra-scan", LifecyclePhase.Verification, Cra, "Annex I Part II(3)", "Security scan without open critical or high findings", ChecklistCheck.Findings),
            new("cra-evidence", LifecyclePhase.Verification, Cra, "Annex VII", "Every covered requirement has evidence", ChecklistCheck.Evidence),
            new("cra-disclosure", LifecyclePhase.Verification, Cra, "Annex I Part II(5)", "Coordinated vulnerability disclosure policy published", ChecklistCheck.Manual),
            new("iso-testing", LifecyclePhase.Verification, Iso27001, "A.8.29", "Security testing done", ChecklistCheck.Manual),
            new("ai-accuracy", LifecyclePhase.Verification, AiAct, "Art. 15", "Accuracy, robustness and cybersecurity tested", ChecklistCheck.Manual),
            new("opt-pentest", LifecyclePhase.Verification, OptionalTest, "", "Penetration test done", ChecklistCheck.Manual, Optional: true),
            new("opt-fuzzing", LifecyclePhase.Verification, OptionalTest, "", "Fuzz testing done", ChecklistCheck.Manual, Optional: true),
            new("opt-sast", LifecyclePhase.Verification, OptionalTest, "", "Static analysis (SAST) done", ChecklistCheck.Manual, Optional: true),
            new("opt-dast", LifecyclePhase.Verification, OptionalTest, "", "Dynamic analysis (DAST) done", ChecklistCheck.Manual, Optional: true),
            new("opt-load", LifecyclePhase.Verification, OptionalTest, "", "Load test done", ChecklistCheck.Manual, Optional: true),

            // Released
            new("cra-support", LifecyclePhase.Released, Cra, "Art. 13(8)", "Support period set", ChecklistCheck.Support),
            new("cra-techdoc", LifecyclePhase.Released, Cra, "Art. 13(12), Annex VII", "Technical documentation complete", ChecklistCheck.Manual),
            new("cra-declaration", LifecyclePhase.Released, Cra, "Art. 28", "EU declaration of conformity signed", ChecklistCheck.Manual),
            new("cra-ce", LifecyclePhase.Released, Cra, "Art. 30", "CE marking affixed", ChecklistCheck.Manual),
            new("cra-reporting", LifecyclePhase.Released, Cra, "Art. 14", "Responsible for reporting exploited vulnerabilities named", ChecklistCheck.Manual),
            new("iso-risk-review", LifecyclePhase.Released, Iso27001, "8.2", "Yearly risk review planned", ChecklistCheck.Manual),
            new("iso-change", LifecyclePhase.Released, Iso27001, "A.8.32", "Change management in place", ChecklistCheck.Manual),
            new("ai-conformity", LifecyclePhase.Released, AiAct, "Art. 43", "Conformity assessment done", ChecklistCheck.Manual),
            new("ai-instructions", LifecyclePhase.Released, AiAct, "Art. 13", "Instructions for use given", ChecklistCheck.Manual),
            new("ai-monitoring", LifecyclePhase.Released, AiAct, "Art. 72", "Post-market monitoring in place", ChecklistCheck.Manual),

            // End of support
            new("cra-retain", LifecyclePhase.EndOfSupport, Cra, "Art. 13(13)", "Documentation kept for ten years", ChecklistCheck.Retained),
            new("cra-users-told", LifecyclePhase.EndOfSupport, Cra, "Annex II(7)", "Users told that support has ended", ChecklistCheck.Manual),
            new("iso-deletion", LifecyclePhase.EndOfSupport, Iso27001, "A.8.10", "Data deleted or kept as decided", ChecklistCheck.Manual)
        ];

        public static ChecklistItem? Find(string key) => All.FirstOrDefault(i => i.Key == key);

        // Whether a framework (by its short name, e.g. "CRA" or "ISO/IEC 27001:2022") is the item's regulation
        public static bool Follows(string regulation, string frameworkShortName) => regulation switch
        {
            Cra => frameworkShortName == "CRA",
            Iso27001 => frameworkShortName.Contains("27001", StringComparison.Ordinal),
            AiAct => frameworkShortName == "AI Act",
            OptionalTest => true,
            _ => false
        };

        // R2: the items of a project that follows these frameworks
        public static IReadOnlyList<ChecklistItem> For(IEnumerable<string> frameworkShortNames)
        {
            var names = frameworkShortNames.ToList();
            return All.Where(i => i.Optional || names.Any(n => Follows(i.Regulation, n))).ToList();
        }
    }

    // US-047: what a project has decided about one item: ticked by hand (R4), turned off with a reason (R8), or, for an
    // optional test, turned on (R9). One row per project and item; the history is in the activity log.
    public class ProjectChecklistEntry
    {
        public const int MaxTextLength = 1000;
        public const int MaxUrlLength = 2000;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string ItemKey { get; private set; } = "";

        public bool Done { get; private set; }
        public Guid? DoneById { get; private set; }
        public UserRole? DoneRole { get; private set; }
        public DateTime? DoneAt { get; private set; }
        public string? Comment { get; private set; }
        public string? EvidenceUrl { get; private set; }

        public bool TurnedOff { get; private set; }
        public string? OffReason { get; private set; }

        // R9: an optional test the project has chosen
        public bool TurnedOn { get; private set; }

        public DateTime ChangedAt { get; private set; }

        // For EF Core only
        private ProjectChecklistEntry() { }

        public static ProjectChecklistEntry For(Guid projectId, string itemKey, DateTime at)
            => new() { ProjectId = projectId, ItemKey = itemKey, ChangedAt = at };

        public void Tick(Guid userId, UserRole role, string? comment, string? evidenceUrl, DateTime at)
        {
            var url = evidenceUrl?.Trim();
            if (!string.IsNullOrEmpty(url) && (url.Length > MaxUrlLength || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            {
                throw new DomainException("The evidence link must be a web address (http or https)");
            }

            Done = true;
            DoneById = userId;
            DoneRole = role;
            DoneAt = at;
            Comment = Cut(comment);
            EvidenceUrl = string.IsNullOrEmpty(url) ? null : url;
            ChangedAt = at;
        }

        public void Untick(DateTime at)
        {
            Done = false;
            DoneById = null;
            DoneRole = null;
            DoneAt = null;
            Comment = null;
            EvidenceUrl = null;
            ChangedAt = at;
        }

        // R8: off with a reason; it no longer counts as missing
        public void TurnOff(string? reason, DateTime at)
        {
            var cleaned = Cut(reason);
            if (cleaned is null)
            {
                throw new DomainException("Give a reason for turning the item off");
            }

            TurnedOff = true;
            OffReason = cleaned;
            TurnedOn = false;
            ChangedAt = at;
        }

        // R8: on again; R9: an optional test chosen
        public void TurnOn(DateTime at)
        {
            TurnedOff = false;
            OffReason = null;
            TurnedOn = true;
            ChangedAt = at;
        }

        private static string? Cut(string? text)
        {
            var cleaned = text?.Trim();
            return string.IsNullOrEmpty(cleaned) ? null : cleaned.Length <= MaxTextLength ? cleaned : cleaned[..MaxTextLength];
        }
    }

    // US-047: an item's state in the project
    public enum ChecklistState
    {
        Done,
        Missing,
        TurnedOff,
        NotChosen
    }

    // What the tracker knows about the project for the automatic items (R4)
    public sealed record ChecklistFacts(bool ScopeDescribed, IReadOnlySet<string> Unclassified, int Risks, int Frameworks, int NotCovered,
        int WithoutEvidence, bool Scanned, int OpenCriticalOrHigh, bool Sbom, bool SupportPeriod, bool Retained)
    {
        // US-195: how many requirements are covered; "every covered requirement has evidence" needs at least one
        public int Covered { get; init; }
    }

    public sealed record ChecklistLine(ChecklistItem Item, ChecklistState State, ProjectChecklistEntry? Entry);

    // US-047 R2, R4, R8, R9: each item's state. Pure, so it is tested on its own.
    public static class ChecklistRules
    {
        public static ChecklistState State(ChecklistItem item, ProjectChecklistEntry? entry, ChecklistFacts facts)
        {
            if (entry?.TurnedOff == true)
            {
                return ChecklistState.TurnedOff;
            }

            if (item.Optional && entry?.TurnedOn != true)
            {
                return ChecklistState.NotChosen;
            }

            var done = item.Check switch
            {
                ChecklistCheck.Manual => entry?.Done == true,
                ChecklistCheck.Scope => facts.ScopeDescribed,
                ChecklistCheck.Classification => !facts.Unclassified.Contains(item.Regulation),
                ChecklistCheck.Risks => facts.Risks > 0,
                ChecklistCheck.Coverage => facts.Frameworks > 0 && facts.NotCovered == 0,
                ChecklistCheck.Evidence => facts.Frameworks > 0 && facts.Covered > 0 && facts.WithoutEvidence == 0,
                ChecklistCheck.Findings => facts.Scanned && facts.OpenCriticalOrHigh == 0,
                ChecklistCheck.Sbom => facts.Sbom,
                ChecklistCheck.Support => facts.SupportPeriod,
                ChecklistCheck.Retained => facts.Retained,
                _ => false
            };
            return done ? ChecklistState.Done : ChecklistState.Missing;
        }

        public static IReadOnlyList<ChecklistLine> Lines(IEnumerable<ChecklistItem> items, IReadOnlyList<ProjectChecklistEntry> entries, ChecklistFacts facts)
            => items.Select(i =>
            {
                var entry = entries.FirstOrDefault(e => e.ItemKey == i.Key);
                return new ChecklistLine(i, State(i, entry, facts), entry);
            }).ToList();

        // R5: what is missing in the phases a move leaves behind (from the current one up to the new one); nothing when moving back
        public static IReadOnlyList<ChecklistLine> MissingBefore(IReadOnlyList<ChecklistLine> lines, LifecyclePhase from, LifecyclePhase to)
            => to <= from ? [] : lines.Where(l => l.State == ChecklistState.Missing && l.Item.Phase >= from && l.Item.Phase < to).ToList();
    }
}
