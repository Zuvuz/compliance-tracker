using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    // US-208 R4: the kinds of interested parties (ISO/IEC 27001 4.2)
    public enum InterestedPartyKind
    {
        Customer,
        Authority,
        Supplier,
        Employees,
        Owners,
        Other
    }

    // US-208 R4: someone with requirements to the organisation's information security, and which of them the ISMS handles
    // (ISO/IEC 27001 4.2 a–c). Removing one keeps the row with a time (the log says who).
    public class InterestedParty
    {
        public const int MaxNameLength = 200;
        public const int MaxRequirementLength = 500;
        public const int MaxRequirements = 30;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public InterestedPartyKind Kind { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        // Kept as JSON in the party's row
        public IReadOnlyList<PartyRequirement> Requirements { get; private set; } = [];

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private InterestedParty() { }

        public static InterestedParty Create(Guid organizationId, string? name, InterestedPartyKind? kind, IEnumerable<(string? Text, bool Handled)> requirements,
            DateTime at)
        {
            var party = new InterestedParty { OrganizationId = organizationId, CreatedAt = at };
            party.Change(name, kind, requirements);
            return party;
        }

        // R4: a name, a kind and one or more requirements; empty requirement lines are left out
        public void Change(string? name, InterestedPartyKind? kind, IEnumerable<(string? Text, bool Handled)> requirements)
        {
            if (!IsActive)
            {
                throw new DomainException("The interested party has been removed");
            }

            var cleanName = (name ?? "").Trim();
            if (cleanName.Length == 0)
            {
                throw new DomainException("Give the interested party's name");
            }

            if (cleanName.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            if (kind is not { } chosen || !Enum.IsDefined(chosen))
            {
                throw new DomainException("Choose the kind of interested party");
            }

            var lines = requirements
                .Select(r => (Text: (r.Text ?? "").Trim(), r.Handled))
                .Where(r => r.Text.Length > 0)
                .ToList();
            if (lines.Count == 0)
            {
                throw new DomainException("Give at least one requirement of the interested party");
            }

            if (lines.Count > MaxRequirements)
            {
                throw new DomainException($"An interested party can have at most {MaxRequirements} requirements");
            }

            if (lines.Any(r => r.Text.Length > MaxRequirementLength))
            {
                throw new DomainException($"A requirement must be {MaxRequirementLength} characters or fewer");
            }

            if (lines.GroupBy(r => r.Text, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            {
                throw new DomainException("The same requirement is given twice");
            }

            Name = cleanName;
            Kind = chosen;
            Requirements = lines.Select((r, i) => new PartyRequirement(r.Text, r.Handled, i)).ToList();
        }

        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The interested party has been removed already");
            }

            RemovedAt = at;
        }

        // e.g. "2 of 3 handled"
        public int Handled => Requirements.Count(r => r.Handled);
    }

    // R4: one requirement of an interested party, and whether the ISMS handles it. Kept in the party's row as JSON.
    public sealed record PartyRequirement(string Text, bool Handled, int Order);
}
