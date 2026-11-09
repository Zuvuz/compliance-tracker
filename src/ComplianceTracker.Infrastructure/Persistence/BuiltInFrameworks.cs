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

            // US-069: version 2 adds the manufacturer's obligations beyond Annex I (BuiltInCraRequirementsV2).
            // It applies from the first of them, Art. 14 (Article 71(2)), so it is the version new projects get (B41).
            public static readonly Guid Version2Id = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05");
            public static readonly DateOnly Version2AppliesFrom = new(2026, 9, 11);
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
        // US-142: NIS2 and its national and technical layers, all at organisation level
        public static class Nis2
        {
            public static readonly Guid FrameworkId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06");
            public static readonly Guid VersionId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07");
            public const string Name = "NIS2 – Directive (EU) 2022/2555";
            public const string SourceReference = "https://eur-lex.europa.eu/eli/dir/2022/2555/oj";
            public static readonly DateTime PublishedAt = new(2022, 12, 27, 0, 0, 0, DateTimeKind.Utc);

            // Art. 41: the member states apply their laws from 18 October 2024
            public static readonly DateOnly AppliesFrom = new(2024, 10, 18);
        }

        public static class Nis2Denmark
        {
            public static readonly Guid FrameworkId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08");
            public static readonly Guid VersionId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09");
            public const string Name = "NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)";
            public const string Country = "DK";
            public const string SourceReference = "https://www.retsinformation.dk/eli/lta/2025/434";
            public static readonly DateTime PublishedAt = new(2025, 5, 6, 0, 0, 0, DateTimeKind.Utc);

            // § 33, stk. 1
            public static readonly DateOnly AppliesFrom = new(2025, 7, 1);
        }

        public static class Nis2Technical
        {
            public static readonly Guid FrameworkId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a");
            public static readonly Guid VersionId = Guid.Parse("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b");
            public const string Name = "NIS2 technical measures – Implementing Regulation (EU) 2024/2690";
            public const string SourceReference = "https://eur-lex.europa.eu/eli/reg_impl/2024/2690/oj";
            public static readonly DateTime PublishedAt = new(2024, 10, 18, 0, 0, 0, DateTimeKind.Utc);
            public static readonly DateOnly AppliesFrom = new(2024, 10, 18);
        }
    }
}
