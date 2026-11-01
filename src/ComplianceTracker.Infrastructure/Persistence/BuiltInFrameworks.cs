namespace ComplianceTracker.Infrastructure.Persistence
{
    // The freely available regulations that come with the tracker (US-006).
    // They are added to every database by a migration (seed data), with fixed ids so they are
    // the same everywhere. Their requirements are added later with the requirement model (US-007 / US-019).
    // To add another regulation: add it here and in the configurations' HasData, then create a migration.
    // US-035 R7: a new version of a built-in regulation is added the same way, with its start date.
    public static class BuiltInFrameworks
    {
        public static class Cra
        {
            public static readonly Guid FrameworkId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01");
            public static readonly Guid VersionId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02");
            public const string Name = "CRA – Regulation (EU) 2024/2847";
            public const string SourceReference = "https://eur-lex.europa.eu/eli/reg/2024/2847/oj";

            // Published in the Official Journal of the EU
            public static readonly DateTime PublishedAt = new(2024, 11, 20, 0, 0, 0, DateTimeKind.Utc);

            // US-035: Article 71(2), the day the regulation applies from (some articles apply earlier)
            public static readonly DateOnly AppliesFrom = new(2027, 12, 11);
        }

        public static class AiAct
        {
            public static readonly Guid FrameworkId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03");
            public static readonly Guid VersionId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04");
            public const string Name = "AI Act – Regulation (EU) 2024/1689";
            public const string SourceReference = "https://eur-lex.europa.eu/eli/reg/2024/1689/oj";

            // Published in the Official Journal of the EU
            public static readonly DateTime PublishedAt = new(2024, 7, 12, 0, 0, 0, DateTimeKind.Utc);

            // US-035: Article 113, the day the regulation applies from (some articles apply earlier or later)
            public static readonly DateOnly AppliesFrom = new(2026, 8, 2);
        }
    }
}
