using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    public enum ObjectiveDirection
    {
        HigherIsBetter,
        LowerIsBetter
    }

    public enum ObjectiveFrequency
    {
        Monthly,
        Quarterly,
        Yearly
    }

    // US-209: the tracker's own measures, worked out over the projects in the ISMS scope (R5). Null on an objective: manual.
    public enum TrackerMeasure
    {
        RequirementCoverage,
        EvidenceCoverage,
        OpenHighRisks,
        OverdueRiskActions,
        OpenNonconformities,
        OpenSecurityEvents,

        // US-213 R6: the share of the members' confirmations of the current policies, over the whole organisation
        PoliciesRead
    }

    // US-209 R4: how the last value stands against the target
    public enum ObjectiveState
    {
        NotMeasured,
        OnTarget,
        CloseToTarget,
        OffTarget
    }

    // US-209: an information security objective (ISO/IEC 27001 6.2) and how it is measured (9.1). Measured by the tracker
    // itself or by a person. Measurements are never changed (R6).
    public class SecurityObjective
    {
        public const int MaxTextLength = 300;
        public const int MaxUnitLength = 20;
        public const int MaxNoteLength = 500;

        // R4: "within 10 %" of the target
        public const decimal CloseMargin = 0.10m;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Text { get; private set; } = "";
        public Guid OwnerId { get; private set; }
        public TrackerMeasure? Measure { get; private set; }
        public decimal Target { get; private set; }
        public string Unit { get; private set; } = "";
        public ObjectiveDirection Direction { get; private set; }
        public ObjectiveFrequency Frequency { get; private set; }
        public DateOnly? DueOn { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        private readonly List<Measurement> _measurements = [];
        public IReadOnlyList<Measurement> Measurements => _measurements.OrderBy(m => m.On).ThenBy(m => m.At).ToList();

        public bool IsActive => RemovedAt is null;
        public bool IsManual => Measure is null;
        public Measurement? Last => Measurements.LastOrDefault();

        // For EF Core only
        private SecurityObjective() { }

        // R1. A tracker measure brings its own unit.
        public static SecurityObjective Create(Guid organizationId, string? text, Guid? ownerId, TrackerMeasure? measure, decimal? target, string? unit,
            ObjectiveDirection? direction, ObjectiveFrequency? frequency, DateOnly? dueOn, DateTime at)
        {
            if (measure is { } known && !Enum.IsDefined(known))
            {
                throw new DomainException("Choose a measure from the list");
            }

            var objective = new SecurityObjective { OrganizationId = organizationId, Measure = measure, CreatedAt = at };
            objective.Change(text, ownerId, target, unit, direction, frequency, dueOn);
            return objective;
        }

        // R1: returns the names of what changed, for the log. The measure itself stays, so the measurements keep their meaning.
        public IReadOnlyList<string> Change(string? text, Guid? ownerId, decimal? target, string? unit, ObjectiveDirection? direction,
            ObjectiveFrequency? frequency, DateOnly? dueOn)
        {
            RequireActive();
            var cleanText = (text ?? "").Trim();
            if (cleanText.Length == 0)
            {
                throw new DomainException("Describe the objective");
            }

            if (cleanText.Length > MaxTextLength)
            {
                throw new DomainException($"The objective must be {MaxTextLength} characters or fewer");
            }

            if (ownerId is null || ownerId == Guid.Empty)
            {
                throw new DomainException("Choose the owner of the objective");
            }

            if (target is null)
            {
                throw new DomainException("Give the objective a target");
            }

            if (target < 0 || (UnitOf(Measure, unit) == "%" && target > 100))
            {
                throw new DomainException(UnitOf(Measure, unit) == "%" ? "A target in per cent is between 0 and 100" : "The target cannot be negative");
            }

            if (direction is null || !Enum.IsDefined(direction.Value))
            {
                throw new DomainException("Choose whether higher or lower is better");
            }

            if (frequency is null || !Enum.IsDefined(frequency.Value))
            {
                throw new DomainException("Choose how often the objective is measured");
            }

            var cleanUnit = UnitOf(Measure, unit);
            if (cleanUnit.Length > MaxUnitLength)
            {
                throw new DomainException($"The unit must be {MaxUnitLength} characters or fewer");
            }

            var changes = new List<string>();
            if (Text != cleanText) { changes.Add("text"); }
            if (OwnerId != ownerId) { changes.Add("owner"); }
            if (Target != target) { changes.Add("target"); }
            if (Unit != cleanUnit) { changes.Add("unit"); }
            if (Direction != direction) { changes.Add("direction"); }
            if (Frequency != frequency) { changes.Add("frequency"); }
            if (DueOn != dueOn) { changes.Add("deadline"); }

            Text = cleanText;
            OwnerId = ownerId.Value;
            Target = target.Value;
            Unit = cleanUnit;
            Direction = direction.Value;
            Frequency = frequency.Value;
            DueOn = dueOn;
            return changes;
        }

        // R2, R3, R6: a new measurement; null byId is the tracker. A person's measurement of a tracker objective corrects it
        // and needs a note.
        public Measurement Record(decimal? value, DateOnly on, Guid? byId, string? note, DateTime at)
        {
            RequireActive();
            if (value is null)
            {
                throw new DomainException("Give the measured value");
            }

            if (value < 0 || (Unit == "%" && value > 100))
            {
                throw new DomainException(Unit == "%" ? "A value in per cent is between 0 and 100" : "The value cannot be negative");
            }

            var cleanNote = (note ?? "").Trim();
            if (cleanNote.Length > MaxNoteLength)
            {
                throw new DomainException($"The note must be {MaxNoteLength} characters or fewer");
            }

            if (!IsManual && byId is not null && cleanNote.Length == 0)
            {
                throw new DomainException("Say why you correct the tracker's measurement");
            }

            var measurement = new Measurement(Id, on, Math.Round(value.Value, 2), byId, cleanNote.Length == 0 ? null : cleanNote, at);
            _measurements.Add(measurement);
            return measurement;
        }

        // R2, R3: due when the frequency has passed since the last measurement, or never measured
        public DateOnly? NextMeasurementOn => Last is { } last ? Add(last.On, Frequency) : null;

        public bool IsDueForMeasurement(DateOnly today) => IsActive && (NextMeasurementOn is not { } next || next <= today);

        // R4
        public ObjectiveState State => Last is { } last ? StateOf(last.Value) : ObjectiveState.NotMeasured;

        public ObjectiveState StateOf(decimal value)
        {
            if (Direction == ObjectiveDirection.HigherIsBetter)
            {
                return value >= Target ? ObjectiveState.OnTarget
                    : value >= Target * (1 - CloseMargin) ? ObjectiveState.CloseToTarget
                    : ObjectiveState.OffTarget;
            }

            return value <= Target ? ObjectiveState.OnTarget
                : value <= Target * (1 + CloseMargin) ? ObjectiveState.CloseToTarget
                : ObjectiveState.OffTarget;
        }

        public void Remove(DateTime at)
        {
            RequireActive();
            RemovedAt = at;
        }

        public static string UnitOf(TrackerMeasure? measure, string? unit) => measure switch
        {
            TrackerMeasure.RequirementCoverage or TrackerMeasure.EvidenceCoverage or TrackerMeasure.PoliciesRead => "%",
            null => (unit ?? "").Trim(),
            _ => ""
        };

        public static DateOnly Add(DateOnly from, ObjectiveFrequency frequency) => frequency switch
        {
            ObjectiveFrequency.Monthly => from.AddMonths(1),
            ObjectiveFrequency.Quarterly => from.AddMonths(3),
            _ => from.AddYears(1)
        };

        private void RequireActive()
        {
            if (!IsActive)
            {
                throw new DomainException("The objective has been removed");
            }
        }
    }

    // US-209 R6: one measurement; never changed. ById null: the tracker measured it.
    public class Measurement
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ObjectiveId { get; private set; }
        public DateOnly On { get; private set; }
        public decimal Value { get; private set; }
        public Guid? ById { get; private set; }
        public string? Note { get; private set; }
        public DateTime At { get; private set; }

        public bool ByTracker => ById is null;

        // For EF Core only
        private Measurement() { }

        internal Measurement(Guid objectiveId, DateOnly on, decimal value, Guid? byId, string? note, DateTime at)
        {
            ObjectiveId = objectiveId;
            On = on;
            Value = value;
            ById = byId;
            Note = note;
            At = at;
        }
    }
}
