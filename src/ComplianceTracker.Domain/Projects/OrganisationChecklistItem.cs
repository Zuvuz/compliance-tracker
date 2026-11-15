using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Projects
{
    // US-192: an item the organisation adds to a phase's checklist, from its own policy or tied to one of its frameworks.
    // Ticked like a built-in manual item (US-047 R4). Removing hides it from the checklists; ticks stay in the history.
    public class OrganisationChecklistItem
    {
        public const string OwnPolicy = "Own policy";
        public const int MaxTextLength = 300;
        public const int MaxReferenceLength = 100;
        public const string KeyPrefix = "org-";

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public LifecyclePhase Phase { get; private set; }
        public string Text { get; private set; } = "";

        // R2: one of the organisation's frameworks, or null for its own policy; Source is what the badge says
        public Guid? FrameworkId { get; private set; }
        public string Source { get; private set; } = OwnPolicy;
        public string Reference { get; private set; } = "";

        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // The key it is ticked by, e.g. "org-3f2a…"
        public string Key => KeyPrefix + Id.ToString("N");

        // For EF Core only
        private OrganisationChecklistItem() { }

        public static OrganisationChecklistItem Create(Guid organizationId, LifecyclePhase? phase, string? text, Guid? frameworkId, string? source,
            string? reference, DateTime at)
        {
            var item = new OrganisationChecklistItem { OrganizationId = organizationId, CreatedAt = at };
            if (phase is null || !Enum.IsDefined(phase.Value))
            {
                throw new DomainException("Choose a phase");
            }

            item.Phase = phase.Value;
            item.FrameworkId = frameworkId;
            item.Source = frameworkId is null ? OwnPolicy : (source ?? "").Trim() is { Length: > 0 } named ? named : throw new DomainException("Choose a framework");
            item.Change(text, reference);
            return item;
        }

        public void Change(string? text, string? reference)
        {
            if (!IsActive)
            {
                throw new DomainException("The item has been removed");
            }

            var cleaned = (text ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Write what must be in place");
            }

            if (cleaned.Length > MaxTextLength)
            {
                throw new DomainException($"The text must be {MaxTextLength} characters or fewer");
            }

            var cleanReference = (reference ?? "").Trim();
            if (cleanReference.Length > MaxReferenceLength)
            {
                throw new DomainException($"The reference must be {MaxReferenceLength} characters or fewer");
            }

            Text = cleaned;
            Reference = cleanReference;
        }

        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The item has been removed");
            }

            RemovedAt = at;
        }

        // US-047: as a checklist item, ticked by hand
        public ChecklistItem AsItem() => new(Key, Phase, Source, Reference, Text, ChecklistCheck.Manual);

        // R3: tied to a framework, it is shown only in projects that follow it; the own policy everywhere
        public bool AppliesTo(Project project) => FrameworkId is not { } id || project.Frameworks.Any(f => f.FrameworkId == id);
    }
}
