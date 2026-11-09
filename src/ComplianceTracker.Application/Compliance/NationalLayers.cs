namespace ComplianceTracker.Application.Compliance
{
    // US-142 R6: a framework that implements another (the Danish NIS2 law implements NIS2; 2024/2690 details NIS2) is
    // linked to it requirement by requirement through Requirement.Implements. When both are followed, a requirement that
    // is covered on its own makes the matching one on the other side count as covered too, with where it came from.
    // Only own coverage is passed on, so coverage never goes round in a circle.
    public static class NationalLayers
    {
        public static List<FrameworkCompliance> Apply(IReadOnlyList<FrameworkCompliance> frameworks)
        {
            // What each requirement gets from the other side: requirement id -> "NIS2 Art. 21(2)(b)"
            var through = new Dictionary<Guid, string>();
            foreach (var layer in frameworks)
            {
                if (layer.Framework.ImplementsFrameworkId is not { } parentId
                    || frameworks.FirstOrDefault(f => f.Framework.Id == parentId) is not { } parent)
                {
                    continue;
                }

                foreach (var row in layer.Requirements.Where(r => r.Requirement.Implements is not null && r.IsApplicable))
                {
                    foreach (var match in parent.Requirements.Where(p => p.Requirement.Code == row.Requirement.Implements && p.IsApplicable))
                    {
                        if (!row.IsOwnCovered && match.IsOwnCovered)
                        {
                            through.TryAdd(row.Requirement.Id, $"{parent.Framework.ShortName} {match.Requirement.Code}");
                        }

                        if (!match.IsOwnCovered && row.IsOwnCovered)
                        {
                            through.TryAdd(match.Requirement.Id, $"{layer.Framework.ShortName} {row.Requirement.Code}");
                        }
                    }
                }
            }

            return through.Count == 0
                ? frameworks.ToList()
                : frameworks.Select(f => f with
                {
                    Requirements = f.Requirements
                        .Select(r => through.TryGetValue(r.Requirement.Id, out var from) ? r with { CoveredThrough = from } : r)
                        .ToList()
                }).ToList();
        }
    }
}
