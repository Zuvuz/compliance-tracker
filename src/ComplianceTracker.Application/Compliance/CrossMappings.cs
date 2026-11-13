using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-037 R5: an approved "Same" match between two requirements the project follows lets coverage pass from the one that
    // is covered on its own to the one that is not, shown as "CRA I.2(d)". As with the national layers (US-142), only own
    // coverage is passed on, so it never goes round in a circle. "Partly" and "Related" do not count.
    public static class CrossMappings
    {
        public static List<FrameworkCompliance> Apply(IReadOnlyList<FrameworkCompliance> frameworks, IReadOnlyList<RequirementMapping> mappings)
        {
            var rows = frameworks.SelectMany(f => f.Requirements.Select(r => (Framework: f, Row: r))).ToDictionary(x => x.Row.Requirement.Id);
            var through = new Dictionary<Guid, string>();
            foreach (var mapping in mappings.Where(m => m.IsApprovedSame))
            {
                if (!rows.TryGetValue(mapping.FromRequirementId, out var a) || !rows.TryGetValue(mapping.ToRequirementId, out var b)
                    || !a.Row.IsApplicable || !b.Row.IsApplicable)
                {
                    continue;
                }

                if (!a.Row.IsCovered && b.Row.IsOwnCovered)
                {
                    through.TryAdd(a.Row.Requirement.Id, $"{b.Framework.Framework.ShortName} {b.Row.Requirement.Code}");
                }

                if (!b.Row.IsCovered && a.Row.IsOwnCovered)
                {
                    through.TryAdd(b.Row.Requirement.Id, $"{a.Framework.Framework.ShortName} {a.Row.Requirement.Code}");
                }
            }

            return through.Count == 0
                ? frameworks.ToList()
                : frameworks.Select(f => f with
                {
                    Requirements = f.Requirements.Select(r => through.TryGetValue(r.Requirement.Id, out var from) ? r with { CoveredThrough = from } : r).ToList()
                }).ToList();
        }
    }
}
