using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    // US-211 R3: a clause or control is done in the cycle, planned, or missing
    public enum CoverageState
    {
        Missing,
        Planned,
        Done
    }

    // US-211 R4: who owns an Annex A control, set on the Internal audits page (the user's choice)
    public sealed record ControlOwner(string Code, Guid OwnerId);

    // US-211: the organisation's internal audit programme (ISO/IEC 27001 9.2): a cycle of 1–5 years with the planned audits,
    // which together must cover clauses 4–10 and the applicable Annex A controls. One per organisation.
    public partial class AuditProgramme
    {
        public const int MinYears = 1;
        public const int MaxYears = 5;

        // R3: the management system's clauses
        public static readonly IReadOnlyList<string> Clauses = ["4", "5", "6", "7", "8", "9", "10"];

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public DateOnly StartsOn { get; private set; }
        public int Years { get; private set; }
        public List<ControlOwner> ControlOwners { get; private set; } = [];

        private readonly List<PlannedAudit> _audits = [];
        public IReadOnlyList<PlannedAudit> Audits => _audits.OrderBy(a => a.On).ToList();

        // The last day of the cycle
        public DateOnly EndsOn => StartsOn.AddYears(Years).AddDays(-1);

        // For EF Core only
        private AuditProgramme() { }

        public static AuditProgramme Create(Guid organizationId, DateOnly? startsOn, int? years)
        {
            var programme = new AuditProgramme { OrganizationId = organizationId };
            programme.Change(startsOn, years);
            return programme;
        }

        // R1: a start and a length of 1–5 years; a planned audit cannot fall outside the cycle
        public void Change(DateOnly? startsOn, int? years)
        {
            if (startsOn is null)
            {
                throw new DomainException("Give the programme a start date");
            }

            if (years is null or < MinYears or > MaxYears)
            {
                throw new DomainException($"A programme lasts {MinYears} to {MaxYears} years");
            }

            var end = startsOn.Value.AddYears(years.Value).AddDays(-1);
            if (_audits.FirstOrDefault(a => a.On < startsOn || a.On > end) is { } outside)
            {
                throw new DomainException($"The audit planned on {outside.On:dd-MM-yyyy} would fall outside the programme");
            }

            StartsOn = startsOn.Value;
            Years = years.Value;
        }

        // R4: the owner of a control; no owner removes it
        public void SetOwner(string? code, Guid? ownerId)
        {
            var clean = ControlCode(code) ?? throw new DomainException("Write the control as in Annex A, e.g. A.8.13");
            var rest = ControlOwners.Where(o => !string.Equals(o.Code, clean, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ownerId is { } owner && owner != Guid.Empty)
            {
                rest.Add(new ControlOwner(clean, owner));
            }

            ControlOwners = [.. rest.OrderBy(o => o.Code, ControlOrder.Instance)];
        }

        public Guid? OwnerOf(string code) => ControlOwners.FirstOrDefault(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase))?.OwnerId;

        // R4: the controls of the plan the auditor owns
        public IReadOnlyList<string> OwnedBy(Guid? auditorId, IEnumerable<string> controls)
            => auditorId is { } id ? controls.Where(c => OwnerOf(c) == id).ToList() : [];

        // R2, R4. An auditor who owns a control in the plan needs a reason; the message names them and the controls.
        public PlannedAudit Plan(DateOnly? on, IEnumerable<string?>? clauses, IEnumerable<string?>? controls, IEnumerable<Guid>? projectIds,
            Guid? auditorId, string? auditorName, string auditorLabel, string? reason)
        {
            if (on is null)
            {
                throw new DomainException("Give the audit a date");
            }

            if (on < StartsOn || on > EndsOn)
            {
                throw new DomainException($"The audit must be within the programme, {StartsOn:dd-MM-yyyy} to {EndsOn:dd-MM-yyyy}");
            }

            var cleanClauses = (clauses ?? []).Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).Distinct().ToList();
            if (cleanClauses.FirstOrDefault(c => !Clauses.Contains(c)) is { } unknown)
            {
                throw new DomainException($"{unknown} is not a clause; choose from 4 to 10");
            }

            var cleanControls = new List<string>();
            foreach (var control in (controls ?? []).Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var code = ControlCode(control) ?? throw new DomainException($"{control!.Trim()} is not an Annex A control; write e.g. A.8.13");
                if (!cleanControls.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    cleanControls.Add(code);
                }
            }

            if (cleanClauses.Count == 0 && cleanControls.Count == 0)
            {
                throw new DomainException("Choose at least one clause or control to audit");
            }

            var name = (auditorName ?? "").Trim();
            if (auditorId is null && name.Length == 0)
            {
                throw new DomainException("Choose the auditor or write the name of an external one");
            }

            var owned = OwnedBy(auditorId, cleanControls);
            var cleanReason = (reason ?? "").Trim();
            if (owned.Count > 0 && cleanReason.Length == 0)
            {
                throw new DomainException($"{auditorLabel} owns this control: {string.Join(", ", owned)}. Choose another auditor or give the reason to keep the plan");
            }

            var audit = new PlannedAudit(Id, on.Value, cleanClauses.OrderBy(int.Parse).ToList(), [.. cleanControls.OrderBy(c => c, ControlOrder.Instance)],
                (projectIds ?? []).Where(p => p != Guid.Empty).Distinct().ToList(), auditorId, auditorId is null ? name : null,
                owned.Count > 0 ? cleanReason : null);
            _audits.Add(audit);
            return audit;
        }

        public PlannedAudit Remove(Guid auditId)
        {
            var audit = AuditOf(auditId);
            if (audit.IsDone)
            {
                throw new DomainException("A done audit stays in the programme");
            }

            _audits.Remove(audit);
            return audit;
        }

        public PlannedAudit AuditOf(Guid auditId) => _audits.FirstOrDefault(a => a.Id == auditId) ?? throw new DomainException("Planned audit not found");

        // R3: every clause and the given controls, with the planned audits in the cycle
        public IReadOnlyList<(string Item, CoverageState State)> Coverage(IEnumerable<string> controls)
        {
            var items = Clauses.Concat(controls.Concat(_audits.SelectMany(a => a.Controls)).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, ControlOrder.Instance));
            return items.Select(item =>
            {
                var audits = _audits.Where(a => a.Covers(item)).ToList();
                return (item, audits.Any(a => a.IsDone) ? CoverageState.Done : audits.Count > 0 ? CoverageState.Planned : CoverageState.Missing);
            }).ToList();
        }

        // "A.8.13" from "a.8.13 " or "A 8.13"; null when it is not an Annex A control
        public static string? ControlCode(string? text)
        {
            var match = ControlPattern().Match((text ?? "").Trim());
            return match.Success ? $"A.{match.Groups[1].Value}" : null;
        }

        [GeneratedRegex(@"^[Aa][\.\s]?(\d+(?:\.\d+){0,2})$")]
        private static partial Regex ControlPattern();
    }

    // US-211: one planned audit in the programme. R5: done when a recorded audit (US-134) is linked to it.
    public class PlannedAudit
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ProgrammeId { get; private set; }
        public DateOnly On { get; private set; }
        public List<string> Clauses { get; private set; } = [];
        public List<string> Controls { get; private set; } = [];
        public List<Guid> ProjectIds { get; private set; } = [];
        public Guid? AuditorId { get; private set; }
        public string? AuditorName { get; private set; }

        // R4: why the plan keeps an auditor who owns a control in it
        public string? ImpartialityReason { get; private set; }

        public Guid? RecordedAuditId { get; private set; }

        public bool IsDone => RecordedAuditId is not null;

        // For EF Core only
        private PlannedAudit() { }

        internal PlannedAudit(Guid programmeId, DateOnly on, List<string> clauses, List<string> controls, List<Guid> projectIds, Guid? auditorId,
            string? auditorName, string? reason)
        {
            ProgrammeId = programmeId;
            On = on;
            Clauses = clauses;
            Controls = controls;
            ProjectIds = projectIds;
            AuditorId = auditorId;
            AuditorName = auditorName;
            ImpartialityReason = reason;
        }

        public bool Covers(string item) => Clauses.Contains(item) || Controls.Contains(item, StringComparer.OrdinalIgnoreCase);

        // R6
        public bool IsOverdue(DateOnly today) => !IsDone && On < today;

        // R5
        public void MarkDone(Guid recordedAuditId)
        {
            if (IsDone)
            {
                throw new DomainException("The planned audit is done already");
            }

            RecordedAuditId = recordedAuditId;
        }
    }

    // Annex A codes in the standard's order: A.5.2 before A.5.10
    public sealed class ControlOrder : IComparer<string>
    {
        public static readonly ControlOrder Instance = new();

        public int Compare(string? x, string? y)
        {
            var a = Parts(x);
            var b = Parts(y);
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var diff = (i < a.Length ? a[i] : -1).CompareTo(i < b.Length ? b[i] : -1);
                if (diff != 0)
                {
                    return diff;
                }
            }

            return string.CompareOrdinal(x, y);
        }

        private static int[] Parts(string? code)
            => (code ?? "").TrimStart('A', 'a', '.').Split('.').Select(p => int.TryParse(p, out var n) ? n : int.MaxValue).ToArray();
    }
}
