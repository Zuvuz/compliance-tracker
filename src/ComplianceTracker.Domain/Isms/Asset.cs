using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Isms
{
    // US-214 R1: what kind of asset it is
    public enum AssetKind
    {
        Information,
        Software,
        Hardware,
        Service,
        People,
        Location
    }

    // US-214 R2: the organisation's own classification levels, lowest first (ISO/IEC 27001 A.5.12). One per organisation;
    // it starts with four levels the organisation can change.
    public class ClassificationScheme
    {
        public const int MaxLevels = 10;
        public const int MaxLevelLength = 100;

        public static readonly IReadOnlyList<string> DefaultLevels = ["Public", "Internal", "Confidential", "Strictly confidential"];

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // Lowest first; the order is what "highest" means (R4)
        public List<string> Levels { get; private set; } = [];

        // For EF Core only
        private ClassificationScheme() { }

        public static ClassificationScheme Create(Guid organizationId)
            => new() { OrganizationId = organizationId, Levels = [.. DefaultLevels] };

        // A level in use by an asset cannot be removed, so no asset is left with a level the scheme does not have.
        // Returns true when the levels changed.
        public bool Change(IEnumerable<string?>? levels, IEnumerable<string> inUse)
        {
            var clean = (levels ?? []).Select(l => (l ?? "").Trim()).Where(l => l.Length > 0).ToList();
            if (clean.Count == 0)
            {
                throw new DomainException("The scheme needs at least one level");
            }

            if (clean.Count > MaxLevels)
            {
                throw new DomainException($"The scheme can have at most {MaxLevels} levels");
            }

            if (clean.FirstOrDefault(l => l.Length > MaxLevelLength) is { } tooLong)
            {
                throw new DomainException($"A level must be {MaxLevelLength} characters or fewer: {tooLong[..20]}…");
            }

            if (clean.GroupBy(l => l, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
            {
                throw new DomainException($"{twice.Key} is in the scheme twice");
            }

            var missing = inUse.Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(used => !clean.Contains(used, StringComparer.OrdinalIgnoreCase))
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Count > 0)
            {
                throw new DomainException($"{string.Join(", ", missing)} is used by assets; give them another level first");
            }

            if (clean.SequenceEqual(Levels, StringComparer.Ordinal))
            {
                return false;
            }

            Levels = clean;
            return true;
        }

        // The level as the scheme writes it, or null when the scheme does not have it
        public string? Find(string? level)
        {
            var clean = (level ?? "").Trim();
            return Levels.FirstOrDefault(l => string.Equals(l, clean, StringComparison.OrdinalIgnoreCase));
        }

        // -1 when the scheme does not have the level
        public int RankOf(string? level) => Find(level) is { } found ? Levels.IndexOf(found) : -1;

        // R4: the highest of the levels, or null when there are none the scheme knows
        public string? Highest(IEnumerable<string?> levels)
            => levels.Select(Find).OfType<string>().OrderByDescending(RankOf).FirstOrDefault();
    }

    // US-214: an information asset or a system that holds information (ISO/IEC 27001 A.5.9), with an owner and a
    // classification, the projects it belongs to, the suppliers that host or deliver it, and the risks that concern it.
    // Removing keeps the row, so the history stays.
    public class Asset
    {
        public const int MaxNameLength = 200;
        public const int MaxDescriptionLength = 1000;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public AssetKind Kind { get; private set; }
        public string? Description { get; private set; }
        public Guid OwnerId { get; private set; }
        public string Classification { get; private set; } = "";

        // R5: listed for GDPR art. 30
        public bool HoldsPersonalData { get; private set; }

        public List<Guid> ProjectIds { get; private set; } = [];
        public List<Guid> SupplierIds { get; private set; } = [];
        public List<Guid> RiskIds { get; private set; } = [];

        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private Asset() { }

        public static Asset Create(Guid organizationId, string? name, AssetKind? kind, Guid? ownerId, string? classification, bool holdsPersonalData,
            string? description, ClassificationScheme scheme, DateTime at)
        {
            var asset = new Asset { OrganizationId = organizationId, CreatedAt = at };
            asset.Change(name, kind, ownerId, classification, holdsPersonalData, description, scheme);
            return asset;
        }

        // R1, R2: returns the names of the fields that changed, for the log
        public IReadOnlyList<string> Change(string? name, AssetKind? kind, Guid? ownerId, string? classification, bool holdsPersonalData,
            string? description, ClassificationScheme scheme)
        {
            RequireActive();
            var cleanName = (name ?? "").Trim();
            if (cleanName.Length == 0)
            {
                throw new DomainException("Give the asset a name");
            }

            if (cleanName.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            if (kind is null || !Enum.IsDefined(kind.Value))
            {
                throw new DomainException("Choose what kind of asset it is");
            }

            if (ownerId is null || ownerId == Guid.Empty)
            {
                throw new DomainException("Choose the owner of the asset");
            }

            var level = scheme.Find(classification)
                ?? throw new DomainException(string.IsNullOrWhiteSpace(classification)
                    ? "Choose the classification of the asset"
                    : $"{classification.Trim()} is not a level in the classification scheme");

            var cleanDescription = (description ?? "").Trim();
            if (cleanDescription.Length > MaxDescriptionLength)
            {
                throw new DomainException($"The description must be {MaxDescriptionLength} characters or fewer");
            }

            var changes = new List<string>();
            if (Name != cleanName) { changes.Add("name"); }
            if (Kind != kind.Value) { changes.Add("kind"); }
            if (OwnerId != ownerId.Value) { changes.Add("owner"); }
            if (Classification != level) { changes.Add("classification"); }
            if (HoldsPersonalData != holdsPersonalData) { changes.Add("personal data"); }
            var newDescription = cleanDescription.Length == 0 ? null : cleanDescription;
            if (Description != newDescription) { changes.Add("description"); }

            Name = cleanName;
            Kind = kind.Value;
            OwnerId = ownerId.Value;
            Classification = level;
            HoldsPersonalData = holdsPersonalData;
            Description = newDescription;
            return changes;
        }

        // The projects, suppliers and risks the asset is linked to; returns true when any changed. An asset can be in
        // several projects.
        public bool Link(IEnumerable<Guid>? projectIds, IEnumerable<Guid>? supplierIds, IEnumerable<Guid>? riskIds)
        {
            RequireActive();
            var projects = Clean(projectIds);
            var suppliers = Clean(supplierIds);
            var risks = Clean(riskIds);
            var changed = !Same(ProjectIds, projects) || !Same(SupplierIds, suppliers) || !Same(RiskIds, risks);
            ProjectIds = projects;
            SupplierIds = suppliers;
            RiskIds = risks;
            return changed;
        }

        // R3: the owner is no longer an active member
        public bool HasOwner(IEnumerable<Guid> activeMemberIds) => activeMemberIds.Contains(OwnerId);

        public void Remove(DateTime at)
        {
            RequireActive();
            RemovedAt = at;
        }

        private void RequireActive()
        {
            if (!IsActive)
            {
                throw new DomainException("The asset has been removed");
            }
        }

        private static List<Guid> Clean(IEnumerable<Guid>? ids) => (ids ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

        private static bool Same(List<Guid> a, List<Guid> b) => a.Count == b.Count && !a.Except(b).Any();
    }
}
